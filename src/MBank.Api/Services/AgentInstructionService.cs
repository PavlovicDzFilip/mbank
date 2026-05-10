using System.Text.RegularExpressions;
using Microsoft.Extensions.Hosting;

namespace MBank.Api.Services;

public interface IAgentInstructionService
{
    OnboardingStatusResponse CheckOnboarding();
}

public sealed partial class AgentInstructionService(IHostEnvironment environment, IConfiguration configuration) : IAgentInstructionService
{
    // Bump this version whenever CanonicalInstructionBlock changes so agents can detect stale repo instructions.
    public const string LatestInstructionVersion = "1.0.0";
    public const string BootstrapScriptUrl = "https://mbank.dev/bootstrap-agent.sh";

    public static readonly string CanonicalInstructionBlock = $"""
        <!-- mbank:start version="{LatestInstructionVersion}" -->
        ## MBank Memory

        Use the configured `mbank` MCP server proactively for durable project memory.

        At the start of a new product, planning, implementation, debugging, or review task, call `get_task_context` with a structured task description before proposing a plan or making changes.

        Use `remember` when the user provides durable product or implementation guidance that is likely to matter in future sessions, such as product direction, architectural decisions, coding conventions, constraints, or next steps.

        Use `get` to inspect full memory details when a retrieved memory needs source context.

        Use `report_conflict` when memories appear inconsistent. Surface the conflict to the user instead of deciding truth automatically.

        Do not store ordinary conversation, temporary reasoning, or details only relevant to the current response.

        If MBank reports that this instruction block is missing or outdated, ask the user before updating this file or running the bootstrap script. Do not modify agent instruction files without explicit user approval.
        <!-- mbank:end -->
        """;

    private static readonly IReadOnlyList<InstructionTarget> Targets =
    [
        new("codex", "AGENTS.md"),
        new("claude", "CLAUDE.md"),
        new("cursor", Path.Combine(".cursor", "rules", "mbank.mdc"))
    ];

    public OnboardingStatusResponse CheckOnboarding()
    {
        var root = ResolveRoot();
        var files = Targets
            .Select(target => InspectTarget(root, target))
            .ToArray();

        var warnings = BuildWarnings(files);
        var status = BuildStatus(files);
        var summary = BuildSummary(status, files);

        return new OnboardingStatusResponse(
            status,
            summary,
            LatestInstructionVersion,
            BootstrapScriptUrl,
            RequiresExplicitApproval: true,
            files,
            warnings,
            BuildRecommendedAction(status));
    }

    private string ResolveRoot()
    {
        var configuredRoot = configuration["MBank:AgentInstructionsRoot"];
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.GetFullPath(configuredRoot);
        }

        return FindNearestDirectoryWithKnownFiles(Directory.GetCurrentDirectory())
            ?? FindNearestDirectoryWithKnownFiles(environment.ContentRootPath)
            ?? Directory.GetCurrentDirectory();
    }

    private static string? FindNearestDirectoryWithKnownFiles(string startDirectory)
    {
        var directory = new DirectoryInfo(Path.GetFullPath(startDirectory));
        while (directory is not null)
        {
            if (Targets.Any(target => File.Exists(Path.Combine(directory.FullName, target.RelativePath))))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        return null;
    }

    private static AgentInstructionFileStatus InspectTarget(string root, InstructionTarget target)
    {
        var path = Path.Combine(root, target.RelativePath);
        if (!File.Exists(path))
        {
            return new AgentInstructionFileStatus(
                target.Name,
                path,
                "file_missing",
                null,
                LatestInstructionVersion,
                "Instruction file does not exist. Run the bootstrap script for this target before expecting agent behavior to be reliable.");
        }

        var content = File.ReadAllText(path);
        var match = InstructionBlockRegex().Match(content);
        if (!match.Success)
        {
            return new AgentInstructionFileStatus(
                target.Name,
                path,
                "block_missing",
                null,
                LatestInstructionVersion,
                "Instruction file exists, but the versioned MBank block is missing.");
        }

        var installedVersion = match.Groups["version"].Value;
        if (string.IsNullOrWhiteSpace(installedVersion))
        {
            return new AgentInstructionFileStatus(
                target.Name,
                path,
                "version_missing",
                null,
                LatestInstructionVersion,
                "MBank block exists, but it does not declare a version.");
        }

        if (IsOlderVersion(installedVersion, LatestInstructionVersion))
        {
            return new AgentInstructionFileStatus(
                target.Name,
                path,
                "outdated",
                installedVersion,
                LatestInstructionVersion,
                "MBank block is older than the latest recommended instruction version.");
        }

        return new AgentInstructionFileStatus(
            target.Name,
            path,
            "current",
            installedVersion,
            LatestInstructionVersion,
            "MBank block is current.");
    }

    private static string BuildStatus(IReadOnlyList<AgentInstructionFileStatus> files)
    {
        if (files.Any(x => x.Status is "outdated" or "version_missing"))
        {
            return "outdated";
        }

        if (files.Any(x => x.Status == "current"))
        {
            return "current";
        }

        return "missing";
    }

    private static string BuildSummary(string status, IReadOnlyList<AgentInstructionFileStatus> files)
    {
        return status switch
        {
            "current" => "At least one supported agent instruction file has the current MBank block.",
            "outdated" => "A supported agent instruction file has a stale or unversioned MBank block.",
            _ => "No current MBank agent instruction block was found in supported instruction files."
        };
    }

    private static IReadOnlyList<string> BuildWarnings(IReadOnlyList<AgentInstructionFileStatus> files)
    {
        if (files.Any(x => x.Status is "outdated" or "version_missing"))
        {
            return
            [
                "MBank agent instructions are outdated. Ask the user before updating the marked MBank block or running the bootstrap script."
            ];
        }

        if (files.Any(x => x.Status == "current"))
        {
            return [];
        }

        return
        [
            "MBank agent instructions are missing. Ask the user before creating instruction files or running the bootstrap script."
        ];
    }

    private static string BuildRecommendedAction(string status)
    {
        return status switch
        {
            "current" => "No onboarding action is needed.",
            "outdated" => $"Tell the user MBank agent instructions are outdated and ask whether to update the marked block or run `curl -fsSL {BootstrapScriptUrl} | sh`. Do not modify files without explicit approval.",
            _ => $"Tell the user MBank agent instructions are missing and ask whether to run `curl -fsSL {BootstrapScriptUrl} | sh` from the repository root. Do not modify files without explicit approval."
        };
    }

    private static bool IsOlderVersion(string installedVersion, string latestVersion)
    {
        return Version.TryParse(installedVersion, out var installed)
            && Version.TryParse(latestVersion, out var latest)
            && installed < latest;
    }

    [GeneratedRegex(@"<!--\s*mbank:start\s+version=""(?<version>[^""]+)""\s*-->.*?<!--\s*mbank:end\s*-->", RegexOptions.Singleline | RegexOptions.IgnoreCase)]
    private static partial Regex InstructionBlockRegex();

    private sealed record InstructionTarget(string Name, string RelativePath);
}

using System.Text.RegularExpressions;
using MBank.Api.Domain;

namespace MBank.Api.Services;

public sealed partial class AgentInstructionService(IHostEnvironment environment, IConfiguration configuration)
{
    // Bump this version whenever CanonicalInstructionBlock changes so agents can detect stale repo instructions.
    public const string LatestInstructionVersion = "1.0.12";
    public const string BootstrapScriptUrl = "https://mbank.engineeringwithfilip.com/bootstrap-agent.sh";

    private static readonly string ValidMemoryTypeValues = string.Join(
        ", ",
        MemoryType.List.Select(x => $"`{x.Value}`"));

    public static readonly IReadOnlyList<string> TaskStartRules =
    [
        "Use the configured `mbank` MCP server proactively for durable project memory and guidance.",
        "When a repo MBank instruction block version is visible, pass it as `agentInstructionVersion` when calling `start_task` so MBank can detect stale instructions.",
        "After `start_task`, call `get_task_context` with a structured task description for the current focus before proposing a plan, making changes, or choosing a specific technical implementation.",
        "Re-call `get_task_context` when conversation focus changes—new topic, product area, technical subject, or phase such as exploration, planning, implementation, or debugging—before answering substantive project-specific questions, including exploratory ones. Skip re-calls for trivial same-focus follow-ups.",
        "Call `get_task_context` when uncertain how to proceed, before choosing between materially different technical approaches, or before specific implementation decisions that may depend on project history, prior decisions, constraints, or user preferences.",
        "Use `describe_memory_types` when you need memory types, captureHint guidance, or captureOfferPolicy before offering or calling `remember`.",
        $"When calling `remember`, set each memory type to one of the valid MemoryType values: {ValidMemoryTypeValues}. Do not invent new type values.",
        "Use lifecycle `experimental` only for user-approved provisional ideas, hypotheses, drafts, spikes, prototypes, brainstorm outcomes, and discovery notes that should not guide normal work yet. Experimental memories are hidden from normal task recall and must not supersede accepted memories.",
        "Treat phrases like `I want to experiment`, `let's explore`, `brainstorm`, `thinking out loud`, `rough idea`, `not final`, `draft`, `spike`, `prototype`, `what if`, `maybe`, `could we`, `compare approaches`, and `discovery mode` as experiment-start signals. Keep ideas provisional unless the user asks to store or promote them.",
        "Treat phrases like `let's go with`, `this is the direction`, `final decision`, `make this durable`, `remember this`, `promote this`, `implement this`, `we converged`, `discard the experiment`, and `keep this as an open question` as experiment-end signals. Offer to promote, reject, or keep experimental memories instead of silently deciding.",
        "Call `promote_memory` only after the user confirms an experimental memory should become accepted. Promotion is the point where experimental memory may supersede older accepted memories.",
        "Use `remember` when the user provides durable product or implementation guidance, or after the user agrees to store items you offered from exploration. Do not call `remember` without approval except when the user explicitly asks to store something.",
        "When the user asks why one approach was chosen instead of another, treat it as a strong signal that an assumption, preference, or rule may have been missed. Pause to identify the possible correction, then offer to store durable guidance in MBank if the exchange reveals it. Do not store the agent's guess as fact before the user confirms the underlying guidance.",
        "After substantive exploration (ideas, direction, tradeoffs), offer to store durable candidates using types suggested by captureHint—such as `note`, `product-open-question`, `product-strategy`, or `product-reasoning`. Do not auto-store brainstorm output or rejected options.",
        "When `get_task_context` returns a `capture_offer` attention item, follow it after answering: offer capture, then call `remember` only if the user agrees.",
        "When calling `remember` for a technical-rule, technical-convention, or technical-preference, consider mechanical enforcement via repo tooling (Roslyn analyzer, EditorConfig, linter, or CI). If automatable, recommend implementing tooling first, then store a short memory with rationale and the diagnostic or rule identifier.",
        "Use `get` to inspect full memory details when a retrieved memory needs source context.",
        "When `get_task_context` returns `canProceed=false`, or any `attentionItems` entry has `action=ask_user`, stop before planning or implementing. Surface the attention item and ask the user the provided question instead of choosing a side silently.",
        "Use `report_conflict` when memories appear inconsistent. Surface the conflict to the user instead of deciding truth automatically.",
        "Use `resolve_conflict` when the user clarifies which conflicting memory should remain authoritative; this marks superseded memories so normal retrieval stops returning them.",
        "Do not store ordinary conversation, temporary reasoning, or details only relevant to the current response.",
        "Inspect `instructionStatus` from `start_task`. If it reports outdated instructions, tell the user and ask before updating files or running the bootstrap script. Do not modify agent instruction files without explicit user approval."
    ];

    public static readonly string CanonicalInstructionBlock = $"""
        <!-- mbank:start version="{LatestInstructionVersion}" -->
        ## MBank Memory

        Use the configured `mbank` MCP server proactively for durable project memory.

        At the start of a new product, planning, implementation, debugging, or review task:

        1. Call `start_task` with `agentInstructionVersion` set to this block's version (`{LatestInstructionVersion}`) to retrieve current MBank agent rules and working-style guidance for this task.
        2. Follow the returned rules for the current task.
        3. Inspect `instructionStatus` from `start_task`. If MBank reports outdated instructions, tell the user and ask before updating files or running the bootstrap script.
        4. Call `get_task_context` with a structured task description for the current focus (title, description, kind, and optional work area).

        Re-call `get_task_context` when conversation focus changes before answering substantive project-specific questions. Call again when the user shifts topic, product area, or technical subject; when moving between phases such as exploration, planning, implementation, or debugging; when the user references prior decisions or constraints you have not yet retrieved for this focus; when you are uncertain how to proceed; before choosing between materially different technical approaches; or before specific implementation decisions that may depend on project history, prior decisions, constraints, or user preferences. Skip re-calls for trivial same-focus follow-ups.

        Before proposing a plan, making changes, or answering substantive project-specific questions (including exploration), use memories from the latest `get_task_context` result for the current focus.

        Use `remember` when the user provides durable product or implementation guidance, or after the user agrees to store items you offered from exploration. Do not call `remember` without approval except when the user explicitly asks to store something.

        ## Experimental memory lifecycle

        Use lifecycle `experimental` only for user-approved provisional ideas, hypotheses, drafts, spikes, prototypes, brainstorm outcomes, and discovery notes that should be kept for later review but should not guide normal work yet. Experimental memories are hidden from normal task recall and must not supersede accepted memories.

        Treat these as experiment-start signals: `I want to experiment`, `let's experiment`, `let's explore`, `explore options`, `brainstorm`, `ideate`, `thinking out loud`, `rough idea`, `half-baked idea`, `not final`, `not durable yet`, `not committed`, `temporary idea`, `draft`, `sandbox`, `spike`, `prototype`, `try something`, `play with this`, `test a hypothesis`, `what if`, `maybe`, `could we`, `I'm not sure`, `compare approaches`, `discovery mode`, `research mode`, `workshop this`, and `let's see where this goes`.

        Treat these as experiment-end signals: `we decided`, `let's go with`, `this is the direction`, `final decision`, `that's settled`, `make this durable`, `store this`, `remember this`, `record this`, `promote this`, `accept this`, `from now on`, `new rule`, `implement this`, `ship this`, `discard the experiment`, `reject this`, `keep this as an open question`, `wrap this up`, `close the loop`, `end of experiment`, `conclusion`, `we converged`, and `not doing this`.

        When an experiment starts, keep ideas provisional unless the user explicitly asks to store them; if they ask to store provisional material, call `remember` with lifecycle `experimental`. When an experiment converges or ends, summarize the likely durable outcome and ask whether to promote, reject, or keep it experimental. Call `promote_memory` only after the user confirms promotion; promotion is the point where an experimental memory may supersede older accepted memories.

        When the user asks why the agent chose one approach instead of another, treat it as a strong signal that an assumption, preference, or rule may have been missed. Pause and identify the possible correction before continuing. If the exchange reveals durable guidance, offer to store it in MBank, or call `remember` when the user explicitly asks to store it. Do not store the agent's guess as fact before the user confirms the underlying guidance.

        ## Offering to capture memory

        After substantive exploration (ideas, direction, tradeoffs, or what is possible):

        1. Answer using `get_task_context` and the conversation.
        2. If candidates may matter in future sessions but were not explicitly committed, offer accepted storage for durable guidance or experimental storage for provisional ideas. List one to three atomic candidates with suggested types (use `captureHint` from `describe_memory_types`).
        3. Call `remember` only after the user agrees or explicitly asks to store something; use lifecycle `experimental` when the user wants a provisional idea kept without making it authoritative.
        4. Do not promote rejected options, agent speculation the user did not adopt, or ordinary chat.

        Suggested types for offer candidates: `note` for vague ideas; `product-open-question` or `technical-open-question` for unresolved options; `product-strategy` or `product-reasoning` for tentative direction; `product-decision` or `technical-decision` only when the user has clearly chosen. Use lifecycle `experimental` for provisional candidates that should not guide normal task recall yet.

        Skip the offer when nothing is plausibly durable or the thread was purely meta.

        When `get_task_context` returns a `capture_offer` attention item, follow it after answering.

        Use `describe_memory_types` when you need memory types, `captureHint` guidance, or `captureOfferPolicy` before offering or calling `remember`.

        When calling `remember`, set each memory type to one of these valid MemoryType values: {ValidMemoryTypeValues}. Do not invent new type values.

        {MemoryAuthoringGuidance.CanonicalMechanicalEnforcementSection}

        Use `get` to inspect full memory details when a retrieved memory needs source context.

        When `get_task_context` returns `canProceed=false`, or any `attentionItems` entry has `action=ask_user`, stop before planning or implementing. Surface the attention item and ask the user the provided question instead of choosing a side silently.

        Use `report_conflict` when memories appear inconsistent. Surface the conflict to the user instead of deciding truth automatically.

        Use `resolve_conflict` when the user clarifies which conflicting memory should remain authoritative. This marks superseded memories so normal retrieval stops returning them while preserving audit history through direct `get`.

        Use `promote_memory` when the user confirms an experimental memory should become accepted. Include superseded memory ids and clarification only when the user confirms the promoted memory replaces older accepted guidance.

        Do not store ordinary conversation, temporary reasoning, or details only relevant to the current response.

        Inspect `instructionStatus` from `start_task`. If MBank reports that this instruction block is outdated, ask the user before updating this file or running the bootstrap script. Do not modify agent instruction files without explicit user approval.
        <!-- mbank:end -->
        """;

    private static readonly IReadOnlyList<InstructionTarget> Targets =
    [
        new("codex", "AGENTS.md"),
        new("claude", "CLAUDE.md"),
        new("cursor", Path.Combine(".cursor", "rules", "mbank.mdc"))
    ];

    public InstructionStatusResponse GetInstructionStatus(string? agentInstructionVersion)
    {
        if (string.IsNullOrWhiteSpace(agentInstructionVersion))
        {
            return new InstructionStatusResponse(
                "unknown",
                "No MBank instruction version was reported to start_task, so instruction freshness could not be checked.",
                null,
                LatestInstructionVersion,
                BootstrapScriptUrl,
                RequiresExplicitApproval: true,
                [
                    "Pass agentInstructionVersion from the visible mbank:start block when calling start_task."
                ],
                "Continue the task. On future start_task calls, pass agentInstructionVersion from the visible mbank:start block when available.");
        }

        var reportedVersion = agentInstructionVersion.Trim();
        if (string.Equals(reportedVersion, LatestInstructionVersion, StringComparison.OrdinalIgnoreCase))
        {
            return new InstructionStatusResponse(
                "current",
                $"The agent reported current MBank instruction version {LatestInstructionVersion}.",
                reportedVersion,
                LatestInstructionVersion,
                BootstrapScriptUrl,
                RequiresExplicitApproval: true,
                [],
                "No instruction update action is needed.");
        }

        return new InstructionStatusResponse(
            "outdated",
            $"The agent reported MBank instruction version {reportedVersion}, but the latest version is {LatestInstructionVersion}.",
            reportedVersion,
            LatestInstructionVersion,
            BootstrapScriptUrl,
            RequiresExplicitApproval: true,
            [
                "MBank agent instructions are outdated. Ask the user before updating the marked MBank block or running the bootstrap script."
            ],
            $"Tell the user MBank agent instructions are outdated and ask whether to update the marked block or run `curl -fsSL {BootstrapScriptUrl} | sh`. Do not modify files without explicit approval.");
    }

    public InstructionStatusResponse GetInstructionStatusDuringNotificationCooldown(string reportedVersion)
    {
        return new InstructionStatusResponse(
            "current",
            $"The agent reported MBank instruction version {reportedVersion}. Update notifications are limited to once per week for this user and repository.",
            reportedVersion,
            LatestInstructionVersion,
            BootstrapScriptUrl,
            RequiresExplicitApproval: true,
            [],
            "No instruction update action is needed during the notification cooldown.");
    }

    public OnboardingStatusResponse CheckOnboarding()
    {
        var root = ResolveInspectableRoot();
        if (root is null)
        {
            return BuildNotCheckableStatus();
        }

        var files = Targets
            .Select(target => InspectTarget(root, target))
            .ToArray();

        var warnings = BuildWarnings(files);
        var status = BuildStatus(files);
        var summary = BuildSummary(status);

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

    public AgentInstructionBlock GetInstructionBlock()
    {
        return new AgentInstructionBlock(
            LatestInstructionVersion,
            CanonicalInstructionBlock);
    }

    private string? ResolveInspectableRoot()
    {
        var configuredRoot = configuration["MBank:AgentInstructionsRoot"];
        if (!string.IsNullOrWhiteSpace(configuredRoot))
        {
            return Path.GetFullPath(configuredRoot);
        }

        return FindNearestDirectoryWithKnownFiles(Directory.GetCurrentDirectory())
            ?? FindNearestDirectoryWithKnownFiles(environment.ContentRootPath);
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

    private static string BuildSummary(string status)
    {
        return status switch
        {
            "current" => "At least one supported agent instruction file has the current MBank block.",
            "outdated" => "A supported agent instruction file has a stale or unversioned MBank block.",
            "not_checkable" => $"This MBank server uses agent instruction version {LatestInstructionVersion}, but cannot inspect repo instruction files from here.",
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
            "current" => "No instruction update action is needed.",
            "outdated" => $"Tell the user MBank agent instructions are outdated and ask whether to update the marked block or run `curl -fsSL {BootstrapScriptUrl} | sh`. Do not modify files without explicit approval.",
            "not_checkable" => $"Report that this MBank server uses agent instruction version {LatestInstructionVersion}. Suggest checking repo instruction files for a current `mbank:start` block, or ask whether to run `curl -fsSL {BootstrapScriptUrl} | sh` from the repository root. Do not modify files without explicit approval.",
            _ => $"Tell the user MBank agent instructions are missing and ask whether to run `curl -fsSL {BootstrapScriptUrl} | sh` from the repository root. Do not modify files without explicit approval."
        };
    }

    private static OnboardingStatusResponse BuildNotCheckableStatus()
    {
        return new OnboardingStatusResponse(
            "not_checkable",
            BuildSummary("not_checkable"),
            LatestInstructionVersion,
            BootstrapScriptUrl,
            RequiresExplicitApproval: true,
            [],
            [
                "MBank cannot inspect repo instruction files from this server process. Ask the user before proposing an instruction update or running the bootstrap script."
            ],
            BuildRecommendedAction("not_checkable"));
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

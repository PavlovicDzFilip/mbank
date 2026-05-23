using System.IO;
using MBank.Api.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace MBank.Tests;

public sealed class AgentInstructionServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "mbank-agent-instructions", Guid.NewGuid().ToString("N"));

    public AgentInstructionServiceTests()
    {
        Directory.CreateDirectory(_root);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Fact]
    public void CheckOnboarding_reports_current_versioned_block()
    {
        File.WriteAllText(Path.Combine(_root, "AGENTS.md"), AgentInstructionService.CanonicalInstructionBlock);
        var service = CreateService();

        var status = service.CheckOnboarding();

        Assert.Equal("current", status.Status);
        Assert.Empty(status.Warnings);
        var codex = status.Files.Single(x => x.Target == "codex");
        Assert.Equal("current", codex.Status);
        Assert.Equal(AgentInstructionService.LatestInstructionVersion, codex.InstalledVersion);
        Assert.True(status.RequiresExplicitApproval);
    }

    [Fact]
    public void Instruction_block_directs_agents_to_start_task_before_task_context()
    {
        var service = CreateService();

        var block = service.GetInstructionBlock();

        Assert.Equal(AgentInstructionService.LatestInstructionVersion, block.Version);
        Assert.Contains("start_task", block.Content);
        Assert.Contains("get_task_context", block.Content);
        Assert.Contains("resolve_conflict", block.Content);
        Assert.Contains(AgentInstructionService.TaskStartRules, x =>
            x.Contains("get_task_context", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(AgentInstructionService.TaskStartRules, x =>
            x.Contains("resolve_conflict", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Re-call `get_task_context`", block.Content, StringComparison.Ordinal);
        Assert.Contains(AgentInstructionService.TaskStartRules, x =>
            x.Contains("Re-call `get_task_context`", StringComparison.Ordinal));
        Assert.Contains("Offering to capture memory", block.Content, StringComparison.Ordinal);
        Assert.Contains("capture_offer", block.Content, StringComparison.Ordinal);
        Assert.Contains(AgentInstructionService.TaskStartRules, x =>
            x.Contains("capture_offer", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("Mechanical enforcement for coding rules", block.Content, StringComparison.Ordinal);
        Assert.Contains(AgentInstructionService.TaskStartRules, x =>
            x.Contains("Roslyn analyzer", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void CheckOnboarding_reports_outdated_versioned_block()
    {
        var oldBlock = AgentInstructionService.CanonicalInstructionBlock.Replace(
            $"version=\"{AgentInstructionService.LatestInstructionVersion}\"",
            "version=\"0.9.0\"",
            StringComparison.Ordinal);
        File.WriteAllText(Path.Combine(_root, "AGENTS.md"), oldBlock);
        var service = CreateService();

        var status = service.CheckOnboarding();

        Assert.Equal("outdated", status.Status);
        Assert.Contains(status.Warnings, x => x.Contains("outdated", StringComparison.OrdinalIgnoreCase));
        var codex = status.Files.Single(x => x.Target == "codex");
        Assert.Equal("outdated", codex.Status);
        Assert.Equal("0.9.0", codex.InstalledVersion);
        Assert.Contains("explicit approval", status.RecommendedAction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CheckOnboarding_reports_missing_block_without_file_mutation()
    {
        var agentsPath = Path.Combine(_root, "AGENTS.md");
        File.WriteAllText(agentsPath, "# AGENTS.md\n\nExisting repo instructions.\n");
        var before = File.ReadAllText(agentsPath);
        var service = CreateService();

        var status = service.CheckOnboarding();

        Assert.Equal("missing", status.Status);
        Assert.Contains(status.Warnings, x => x.Contains("missing", StringComparison.OrdinalIgnoreCase));
        Assert.Equal("block_missing", status.Files.Single(x => x.Target == "codex").Status);
        Assert.Equal(before, File.ReadAllText(agentsPath));
    }

    [Fact]
    public void Bootstrap_scripts_default_to_all_targets_and_public_server()
    {
        var repoRoot = FindRepositoryRoot();
        var shell = File.ReadAllText(Path.Combine(repoRoot, "scripts", "bootstrap-agent.sh"));
        var powershell = File.ReadAllText(Path.Combine(repoRoot, "scripts", "bootstrap-agent.ps1"));

        Assert.Contains("DEFAULT_CLIENT=\"all\"", shell);
        Assert.Contains("DEFAULT_SERVER_URL=\"https://mbank.engineeringwithfilip.com\"", shell);
        Assert.DoesNotContain("\r\n", shell);
        Assert.Contains("AGENTS.md", shell);
        Assert.Contains("CLAUDE.md", shell);
        Assert.Contains(".cursor/rules/mbank.mdc", shell);
        Assert.Contains("printed_mbank", shell);

        Assert.Contains("[string] $Client = \"all\"", powershell);
        Assert.Contains("[string] $ServerUrl = \"https://mbank.engineeringwithfilip.com\"", powershell);
        Assert.Contains("AGENTS.md", powershell);
        Assert.Contains("CLAUDE.md", powershell);
        Assert.Contains(".cursor/rules/mbank.mdc", powershell);
        Assert.Contains("$replaced = $false", powershell);
    }

    private AgentInstructionService CreateService()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["MBank:AgentInstructionsRoot"] = _root
            })
            .Build();

        return new AgentInstructionService(new TestHostEnvironment(_root), configuration);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(directory.FullName, "MBank.slnx")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Could not find repository root.");
    }

    private sealed class TestHostEnvironment(string contentRootPath) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = Environments.Development;

        public string ApplicationName { get; set; } = "MBank.Tests";

        public string ContentRootPath { get; set; } = contentRootPath;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}

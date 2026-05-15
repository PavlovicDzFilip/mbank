using System.Net;
using MBank.Api.Auth;
using MBank.Api.Data;
using MBank.Api.Domain;
using MBank.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MBank.Tests;

public sealed class RetrievalQualityTests(MBankWebApplicationFactory factory)
    : IClassFixture<MBankWebApplicationFactory>, IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await factory.ResetDatabaseAsync();
    }

    public Task DisposeAsync()
    {
        return Task.CompletedTask;
    }

    [Fact]
    public async Task WebApplicationFactory_starts_the_app()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("MBank", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Onboarding_endpoint_returns_canonical_agent_instruction_block()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/onboarding/agent-instructions");
        var block = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains($"<!-- mbank:start version=\"{AgentInstructionService.LatestInstructionVersion}\" -->", block);
        Assert.Contains("## MBank Memory", block);
        Assert.Contains("start_task", block);
        Assert.Contains("get_task_context", block);
    }

    [Fact]
    public async Task StartTask_returns_canonical_rules_and_latest_working_style_guidance()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MemoryService>();
        await service.RememberAsync(new RememberRequest(
            [
                new MemoryDraft(
                    "preference",
                    "The user prefers small, focused commits rather than large bundled commits.",
                    "Small commit preference",
                    "user_preference",
                    ["working-style", "commits"],
                    1),
                new MemoryDraft(
                    "product_preference",
                    "MBank should make user working-style preferences easy for agents to retrieve alongside product and technical context at the start of tasks.",
                    "Working-style memory retrieval",
                    "product_area",
                    ["working-style", "retrieval"],
                    1)
            ]),
            CancellationToken.None);

        var response = await service.StartTaskAsync(CancellationToken.None);

        Assert.Contains(response.Rules, x =>
            x.Source == "canonical_instruction"
            && x.Text.Contains("get_task_context", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(response.Rules, x =>
            x.Source == "memory"
            && x.Text.Contains("small, focused commits", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(response.Rules, x =>
            x.Text.StartsWith("MBank should make user working-style preferences", StringComparison.Ordinal));
        Assert.Equal("Call `get_task_context` with this task's title, description, kind, and work area before planning or implementing.", response.NextStep);
        Assert.True(response.Onboarding.RequiresExplicitApproval);
    }

    [Fact]
    public async Task GetTaskContext_only_reads_memories_from_current_project()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MemoryService>();
        var currentScope = scope.ServiceProvider.GetRequiredService<MemoryScopeProvider>();
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();

        await service.RememberAsync(new RememberRequest(
            [
                new MemoryDraft(
                    "decision",
                    "Default project memory should not leak into another project.",
                    "Project isolation",
                    "technical-area",
                    ["isolation"],
                    1)
            ]),
            CancellationToken.None);

        var otherProject = Project.Create("Other Project");
        db.Projects.Add(otherProject);
        await db.SaveChangesAsync();
        currentScope.Set(new AuthenticatedMemoryScope(
            TestDatabase.UserId,
            otherProject.Id,
            AccessTokenPermissions.Crud));

        var response = await service.GetTaskContextAsync(new TaskContextRequest(
            new TaskDescription(
                "Check project isolation",
                "Can this project see the default project memory?",
                "validation"),
            new WorkArea("Project isolation", "technical-area"),
            Limit: 4), CancellationToken.None);

        Assert.Equal("no_context_found", response.Status);
        Assert.Empty(response.StrongMatches);
        Assert.Empty(response.PossibleMatches);
    }

    [Fact]
    public async Task GetTaskContext_rejects_absolute_file_paths()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MemoryService>();

        var error = await Assert.ThrowsAsync<ArgumentException>(() => service.GetTaskContextAsync(new TaskContextRequest(
            new TaskDescription(
                "Validate path handling",
                "Reject absolute local paths so MBank never stores machine-specific repo locations.",
                "validation"),
            new WorkArea("Path validation", "technical-area", Files: ["D:\\MBank\\src\\MBank.Api\\Program.cs"])),
            CancellationToken.None));

        Assert.Contains("absolute local paths are not allowed", error.Message);
        Assert.Contains("repository-relative paths", error.Message);
    }

    [Fact]
    public async Task GetTaskContext_ranks_mvp_scope_guidance_above_noisy_matches()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MemoryService>();
        await SeedStandardMemoriesAsync(service);

        var response = await service.GetTaskContextAsync(new TaskContextRequest(
            new TaskDescription(
                "Decide whether the MVP should add a dashboard",
                "We are considering a Razor UI, billing, permissions, and enterprise controls. Should the MVP still focus on MCP storage and retrieval?",
                "product-planning"),
            new WorkArea("MVP scope", "product_capability", Modules: ["MBank.Api"]),
            Limit: 4), CancellationToken.None);

        Assert.Equal("context_found", response.Status);
        Assert.NotEmpty(response.StrongMatches);
        Assert.Contains("MVP focuses on MCP storage and retrieval", response.StrongMatches[0].Summary);
        Assert.DoesNotContain(response.StrongMatches.Take(2), x => x.Subject == "Enterprise billing");
        Assert.Equal("strong", response.StrongMatches[0].MatchStrength);
        Assert.InRange(response.StrongMatches[0].MatchScore, 0.75, 1);
    }

    [Fact]
    public async Task GetTaskContext_retrieves_launch_working_directory_guidance()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MemoryService>();
        await SeedStandardMemoriesAsync(service);

        var response = await service.GetTaskContextAsync(new TaskContextRequest(
            new TaskDescription(
                "Run MBank.Api from Codex",
                "The API starts but appsettings and user-secrets are missing. Check the launch working directory and visible process guidance.",
                "implementation"),
            new WorkArea("MBank.Api launch", "technical-area", Modules: ["MBank.Api"]),
            Limit: 3), CancellationToken.None);

        Assert.NotEmpty(response.StrongMatches);
        Assert.Contains("working directory", response.StrongMatches[0].Summary);
        Assert.Contains("user-secrets", response.StrongMatches[0].Summary);
        Assert.True(response.CanProceed);
        Assert.DoesNotContain(response.AttentionItems, x => x.Kind == "conflict");
        Assert.DoesNotContain(response.AttentionItems, x => x.Kind == "clarifying_question");
    }

    [Fact]
    public async Task GetTaskContext_includes_full_text_candidates_in_ranked_matches()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MemoryService>();
        await service.RememberAsync(new RememberRequest(
            [
                new MemoryDraft(
                    "technical_convention",
                    "The retrieval test sentinel term is apricotvector. It proves PostgreSQL full-text search participates in ranking.",
                    "Hybrid retrieval",
                    "technical-area",
                    ["full-text", "apricotvector"],
                    1)
            ]),
            CancellationToken.None);

        await service.RememberAsync(new RememberRequest(
            [
                new MemoryDraft(
                    "decision",
                    "A generic unrelated memory about invoices and billing should not satisfy retrieval validation.",
                    "Enterprise billing",
                    "product_capability",
                    ["billing"],
                    0.95)
            ]),
            CancellationToken.None);

        var response = await service.GetTaskContextAsync(new TaskContextRequest(
            new TaskDescription(
                "Find apricotvector retrieval behavior",
                "Does the apricotvector sentinel show that full text search contributes to get_task_context?",
                "validation"),
            new WorkArea("Hybrid retrieval", "technical-area"),
            Limit: 2), CancellationToken.None);

        Assert.NotEmpty(response.StrongMatches);
        Assert.Contains("apricotvector", response.StrongMatches[0].Summary);
    }

    [Fact]
    public async Task GetTaskContext_reports_warning_and_same_subject_conflict()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MemoryService>();
        await SeedStandardMemoriesAsync(service);

        var response = await service.GetTaskContextAsync(new TaskContextRequest(
            new TaskDescription(
                "Reconsider MVP scope",
                "Should we ignore the MCP loop and start UI, permissions, and enterprise workflows now?",
                "product-planning"),
            new WorkArea("MVP scope", "product_capability"),
            Limit: 5), CancellationToken.None);

        Assert.False(response.CanProceed);
        Assert.Contains(response.AttentionItems, x =>
            x.Kind == "warning"
            && x.Action == TaskContextAttentionAction.Notice
            && x.Summary.Contains("Do not start UI work", StringComparison.OrdinalIgnoreCase));
        var conflicts = response.AttentionItems.Where(x => x.Kind == "conflict").ToArray();
        Assert.NotEmpty(conflicts);
        Assert.All(conflicts, conflict =>
        {
            Assert.Equal(TaskContextAttentionAction.AskUser, conflict.Action);
            Assert.Contains("Which one should guide this task?", conflict.Question);
            Assert.NotNull(conflict.MemoryIds);
            Assert.Equal(2, conflict.MemoryIds.Count);
            var existing = response.StrongMatches.Single(x => x.Id == conflict.MemoryIds[0]);
            var current = response.StrongMatches.Single(x => x.Id == conflict.MemoryIds[1]);
            Assert.Equal(existing.Subject, current.Subject);
        });
    }

    [Fact]
    public async Task GetTaskContext_does_not_invent_cross_subject_conflicts()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MemoryService>();
        await SeedStandardMemoriesAsync(service, includeMvpWarning: false);

        var response = await service.GetTaskContextAsync(new TaskContextRequest(
            new TaskDescription(
                "Validate MCP retrieval quality",
                "Check storage, retrieval, launch configuration, and MCP tool context quality in one planning task.",
                "validation"),
            new WorkArea("MCP retrieval validation", "technical-area", Modules: ["MBank.Api", "MCP"]),
            Limit: 5), CancellationToken.None);

        Assert.True(response.StrongMatches.Select(x => x.Subject).Distinct().Count() > 1);
        Assert.True(response.CanProceed);
        Assert.DoesNotContain(response.AttentionItems, x => x.Kind == "conflict");
    }

    [Fact]
    public async Task GetTaskContext_returns_possible_matches_separately_with_bucketed_scores()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MemoryService>();
        await service.RememberAsync(new RememberRequest(
            [
                new MemoryDraft(
                    "decision",
                    "Generic search is not a core v1 MCP workflow unless a clear manual or debug use case appears later.",
                    "MCP tool surface",
                    "technical_area",
                    ["mcp", "tools", "search"],
                    0.98)
            ]),
            CancellationToken.None);

        var response = await service.GetTaskContextAsync(new TaskContextRequest(
            new TaskDescription(
                "Review search behavior",
                "Should the product expose search?",
                "product-planning"),
            Limit: 2), CancellationToken.None);

        Assert.Equal("possible_context_found", response.Status);
        Assert.Empty(response.StrongMatches);
        var possible = Assert.Single(response.PossibleMatches);
        Assert.Equal("possible", possible.MatchStrength);
        Assert.Equal(0.98, possible.MemoryConfidence);
        Assert.InRange(possible.MatchScore, 0.40, 0.74);
        Assert.Contains(response.AttentionItems, x =>
            x.Kind == "possible_matches"
            && x.Action == TaskContextAttentionAction.Notice
            && x.Summary.Contains("Only possible memory matches", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Get_returns_source_evidence_for_stored_memory()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MemoryService>();
        var stored = await service.RememberAsync(new RememberRequest(
            [
                new MemoryDraft(
                    "decision",
                    "Source evidence must be retrievable through get so agents can inspect where a memory came from.",
                    "Memory evidence",
                    "technical-area",
                    ["source", "evidence"],
                    1)
            ],
            new SourceDraft(
                "test_source",
                "This source text proves the memory detail endpoint carries evidence.",
                "https://example.test/source")),
            CancellationToken.None);

        var detail = await service.GetAsync(stored.StoredMemories[0].Id, CancellationToken.None);

        Assert.NotNull(detail);
        Assert.NotNull(detail.Source);
        Assert.Equal("test_source", detail.Source.SourceKind);
        Assert.Contains("proves the memory detail endpoint", detail.Source.SourceText);
    }

    private static async Task SeedStandardMemoriesAsync(MemoryService service, bool includeMvpWarning = true)
    {
        var memories = new List<MemoryDraft>
        {
            new(
                "decision",
                "The MVP focuses on MCP storage and retrieval before UI, team management, auth, billing, permissions, dashboards, and enterprise controls.",
                "MVP scope",
                "product_capability",
                ["mvp", "mcp", "storage", "retrieval", "scope"],
                0.98),
            new(
                "technical_convention",
                "When Codex runs MBank.Api, it must use D:\\MBank\\src\\MBank.Api as the working directory so appsettings and .NET user-secrets are applied correctly.",
                "MBank.Api launch",
                "technical-area",
                ["dotnet", "user-secrets", "launch"],
                1),
            new(
                "technical_convention",
                "When Codex runs the MBank project, start the app in a visible window so the user can see and control the running process.",
                "MBank.Api launch",
                "technical-area",
                ["dotnet", "visible-window", "launch"],
                1),
            new(
                "decision",
                "Generic search is not a core v1 MCP workflow unless a clear manual or debug use case appears later.",
                "MCP tool surface",
                "technical_area",
                ["mcp", "tools", "search"],
                0.98),
            new(
                "validation_result",
                "MBank MCP get_task_context was validated end to end with hybrid retrieval, ranked matches, and no noisy cross-subject conflicts.",
                "MCP retrieval validation",
                "technical-area",
                ["mcp", "validation", "hybrid-retrieval", "rrf"],
                1),
            new(
                "decision",
                "Enterprise billing, organization hierarchy, tenant administration, and audit controls belong to a later product tier.",
                "Enterprise billing",
                "product_capability",
                ["enterprise", "billing", "permissions"],
                0.9)
        };

        if (includeMvpWarning)
        {
            memories.Add(new MemoryDraft(
                "warning",
                "Do not start UI work before the MCP memory loop is proven and covered by tests.",
                "MVP scope",
                "product_capability",
                ["ui", "mcp", "tests"],
                0.9));
        }

        await service.RememberAsync(new RememberRequest(memories), CancellationToken.None);
    }
}

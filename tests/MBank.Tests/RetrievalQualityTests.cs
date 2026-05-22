using System.Net;
using MBank.Api.Auth;
using MBank.Api.Data;
using MBank.Api.Domain;
using MBank.Api.Services;
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
    public async Task Home_redirects_unauthenticated_users_to_login()
    {
        var client = factory.CreateClient(new Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false
        });

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.Redirect, response.StatusCode);
        Assert.Equal("/login", response.Headers.Location?.ToString());
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
                    MemoryType.UserPreference,
                    "The user prefers small, focused commits rather than large bundled commits.",
                    "Small commit preference",
                    ["working-style", "commits"],
                    1),
                new MemoryDraft(
                    MemoryType.ProductStrategy,
                    "MBank should make user working-style preferences easy for agents to retrieve alongside product and technical context at the start of tasks.",
                    "Working-style memory retrieval",
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
        Assert.Equal(
            "Call `get_task_context` with this task's current focus (title, description, kind, and optional work area). Re-call it when conversation focus changes before substantive project-specific answers, including exploration.",
            response.NextStep);
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
                    MemoryType.TechnicalDecision,
                    "Default project memory should not leak into another project.",
                    "Project isolation",
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
            new WorkArea("Project isolation"),
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
            new WorkArea("Path validation", Files: ["D:\\MBank\\src\\MBank.Api\\Program.cs"])),
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
            new WorkArea("MVP scope", Modules: ["MBank.Api"]),
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
            new WorkArea("MBank.Api launch", Modules: ["MBank.Api"]),
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
                    MemoryType.TechnicalConvention,
                    "The retrieval test sentinel term is apricotvector. It proves PostgreSQL full-text search participates in ranking.",
                    "Hybrid retrieval",
                    ["full-text", "apricotvector"],
                    1)
            ]),
            CancellationToken.None);

        await service.RememberAsync(new RememberRequest(
            [
                new MemoryDraft(
                    MemoryType.ProductDecision,
                    "A generic unrelated memory about invoices and billing should not satisfy retrieval validation.",
                    "Enterprise billing",
                    ["billing"],
                    0.95)
            ]),
            CancellationToken.None);

        var response = await service.GetTaskContextAsync(new TaskContextRequest(
            new TaskDescription(
                "Find apricotvector retrieval behavior",
                "Does the apricotvector sentinel show that full text search contributes to get_task_context?",
                "validation"),
            new WorkArea("Hybrid retrieval"),
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
            new WorkArea("MVP scope"),
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
    public async Task GetTaskContext_includes_capture_offer_for_planning_kind()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MemoryService>();

        var response = await service.GetTaskContextAsync(new TaskContextRequest(
            new TaskDescription(
                "Explore capture workflow",
                "What options exist for offering to remember ideas after exploration?",
                "planning"),
            Limit: 3), CancellationToken.None);

        Assert.Contains(response.AttentionItems, x =>
            x.Kind == "capture_offer"
            && x.Action == TaskContextAttentionAction.Notice);
    }

    [Fact]
    public async Task GetTaskContext_skips_capture_offer_for_implementation_kind()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MemoryService>();

        var response = await service.GetTaskContextAsync(new TaskContextRequest(
            new TaskDescription(
                "Implement capture offer",
                "Add capture_offer attention item handling in the API.",
                "implementation"),
            Limit: 3), CancellationToken.None);

        Assert.DoesNotContain(response.AttentionItems, x => x.Kind == "capture_offer");
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
            new WorkArea("MCP retrieval validation", Modules: ["MBank.Api", "MCP"]),
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
                    MemoryType.ProductDecision,
                    "Generic search is not a core v1 MCP workflow unless a clear manual or debug use case appears later.",
                    "MCP tool surface",
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
                    MemoryType.TechnicalDecision,
                    "Source evidence must be retrievable through get so agents can inspect where a memory came from.",
                    "Memory evidence",
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

    [Fact]
    public async Task ResolveConflict_supersedes_losing_memory_without_removing_audit_detail()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<MemoryService>();
        var stored = await service.RememberAsync(new RememberRequest(
            [
                new MemoryDraft(
                    MemoryType.AgentInstruction,
                    "Use the current reproducible docker compose process for MBank infrastructure.",
                    "Infrastructure launch",
                    ["working-style", "launch"],
                    1),
                new MemoryDraft(
                    MemoryType.AgentInstruction,
                    "Use the obsolete one-off container process with the violetmarker sentinel.",
                    "Infrastructure launch",
                    ["working-style", "launch", "violetmarker"],
                    1)
            ]),
            CancellationToken.None);
        var keptMemoryId = stored.StoredMemories[0].Id;
        var supersededMemoryId = stored.StoredMemories[1].Id;

        await service.ReportConflictAsync(new ReportConflictRequest(
            [keptMemoryId, supersededMemoryId],
            "These launch memories disagree."),
            CancellationToken.None);

        var resolution = await service.ResolveConflictAsync(new ResolveConflictRequest(
            keptMemoryId,
            [supersededMemoryId],
            "The user clarified that docker compose is the reproducible process."),
            CancellationToken.None);

        Assert.True(resolution.Resolved);
        Assert.Equal(keptMemoryId, resolution.KeptMemoryId);
        Assert.Contains(supersededMemoryId, resolution.SupersededMemoryIds!);

        var startTask = await service.StartTaskAsync(CancellationToken.None);
        Assert.Contains(startTask.Rules, x =>
            x.MemoryId == keptMemoryId
            && x.Text.Contains("docker compose", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(startTask.Rules, x => x.MemoryId == supersededMemoryId);

        var context = await service.GetTaskContextAsync(new TaskContextRequest(
            new TaskDescription(
                "Check violetmarker launch guidance",
                "Should agents use the violetmarker one-off container launch process?",
                "implementation"),
            new WorkArea("Infrastructure launch"),
            Limit: 4), CancellationToken.None);

        Assert.DoesNotContain(context.StrongMatches, x => x.Id == supersededMemoryId);
        Assert.DoesNotContain(context.PossibleMatches, x => x.Id == supersededMemoryId);

        var keptDetail = await service.GetAsync(keptMemoryId, CancellationToken.None);
        var supersededDetail = await service.GetAsync(supersededMemoryId, CancellationToken.None);

        Assert.NotNull(keptDetail);
        Assert.NotNull(supersededDetail);
        Assert.Equal(MemoryEntry.ActiveStatus, keptDetail.Status);
        Assert.Equal(MemoryEntry.SupersededStatus, supersededDetail.Status);
        Assert.Contains(keptDetail.Links, x =>
            x.MemoryId == supersededMemoryId
            && x.Relation == MemoryLink.SupersedesRelation
            && x.Description!.Contains("docker compose", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task SeedStandardMemoriesAsync(MemoryService service, bool includeMvpWarning = true)
    {
        var memories = new List<MemoryDraft>
        {
            new(
                MemoryType.ProductDecision,
                "The MVP focuses on MCP storage and retrieval before UI, team management, auth, billing, permissions, dashboards, and enterprise controls.",
                "MVP scope",
                ["mvp", "mcp", "storage", "retrieval", "scope"],
                0.98),
            new(
                MemoryType.TechnicalConvention,
                "When Codex runs MBank.Api, it must use D:\\MBank\\src\\MBank.Api as the working directory so appsettings and .NET user-secrets are applied correctly.",
                "MBank.Api launch",
                ["dotnet", "user-secrets", "launch"],
                1),
            new(
                MemoryType.TechnicalConvention,
                "When Codex runs the MBank project, start the app in a visible window so the user can see and control the running process.",
                "MBank.Api launch",
                ["dotnet", "visible-window", "launch"],
                1),
            new(
                MemoryType.ProductDecision,
                "Generic search is not a core v1 MCP workflow unless a clear manual or debug use case appears later.",
                "MCP tool surface",
                ["mcp", "tools", "search"],
                0.98),
            new(
                MemoryType.ValidationResult,
                "MBank MCP get_task_context was validated end to end with hybrid retrieval, ranked matches, and no noisy cross-subject conflicts.",
                "MCP retrieval validation",
                ["mcp", "validation", "hybrid-retrieval", "rrf"],
                1),
            new(
                MemoryType.ProductDecision,
                "Enterprise billing, organization hierarchy, tenant administration, and audit controls belong to a later product tier.",
                "Enterprise billing",
                ["enterprise", "billing", "permissions"],
                0.9)
        };

        if (includeMvpWarning)
        {
            memories.Add(new MemoryDraft(
                MemoryType.ProductRisk,
                "Do not start UI work before the MCP memory loop is proven and covered by tests.",
                "MVP scope",
                ["ui", "mcp", "tests"],
                0.9));
        }

        await service.RememberAsync(new RememberRequest(memories), CancellationToken.None);
    }
}

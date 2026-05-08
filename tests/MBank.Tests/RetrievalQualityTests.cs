using System.Net;
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
    public async Task WebApplicationFactory_starts_the_app()
    {
        var client = factory.CreateClient();

        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("MBank", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task GetTaskContext_ranks_mvp_scope_guidance_above_noisy_matches()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMemoryService>();
        await SeedStandardMemoriesAsync(service);

        var response = await service.GetTaskContextAsync(new TaskContextRequest(
            new TaskDescription(
                "Decide whether the MVP should add a dashboard",
                "We are considering a Razor UI, billing, permissions, and enterprise controls. Should the MVP still focus on MCP storage and retrieval?",
                "product-planning"),
            new WorkArea("MVP scope", "product_capability", Modules: ["MBank.Api"]),
            Limit: 4), CancellationToken.None);

        Assert.Equal("context_found", response.Status);
        Assert.NotEmpty(response.RelevantMemories);
        Assert.Contains("MVP focuses on MCP storage and retrieval", response.RelevantMemories[0].Summary);
        Assert.DoesNotContain(response.RelevantMemories.Take(2), x => x.Subject == "Enterprise billing");
        Assert.Contains("subject match", response.RelevantMemories[0].WhyRelevant);
        Assert.Contains("vector similarity", response.RelevantMemories[0].WhyRelevant);
    }

    [Fact]
    public async Task GetTaskContext_retrieves_launch_working_directory_guidance()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMemoryService>();
        await SeedStandardMemoriesAsync(service);

        var response = await service.GetTaskContextAsync(new TaskContextRequest(
            new TaskDescription(
                "Run MBank.Api from Codex",
                "The API starts but appsettings and user-secrets are missing. Check the launch working directory and visible process guidance.",
                "implementation"),
            new WorkArea("MBank.Api launch", "technical-area", Modules: ["MBank.Api"]),
            Limit: 3), CancellationToken.None);

        Assert.NotEmpty(response.RelevantMemories);
        Assert.Contains("working directory", response.RelevantMemories[0].Summary);
        Assert.Contains("user-secrets", response.RelevantMemories[0].Summary);
        Assert.Empty(response.Conflicts);
        Assert.Empty(response.ClarifyingQuestions);
    }

    [Fact]
    public async Task GetTaskContext_includes_full_text_candidates_in_relevance_reasons()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMemoryService>();
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

        Assert.NotEmpty(response.RelevantMemories);
        Assert.Contains("apricotvector", response.RelevantMemories[0].Summary);
        Assert.Contains("RRF", response.RelevantMemories[0].WhyRelevant);
        Assert.Contains("full-text rank", response.RelevantMemories[0].WhyRelevant);
    }

    [Fact]
    public async Task GetTaskContext_reports_warning_and_same_subject_conflict()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMemoryService>();
        await SeedStandardMemoriesAsync(service);

        var response = await service.GetTaskContextAsync(new TaskContextRequest(
            new TaskDescription(
                "Reconsider MVP scope",
                "Should we ignore the MCP loop and start UI, permissions, and enterprise workflows now?",
                "product-planning"),
            new WorkArea("MVP scope", "product_capability"),
            Limit: 5), CancellationToken.None);

        Assert.Contains(response.Warnings, x => x.Contains("Do not start UI work", StringComparison.OrdinalIgnoreCase));
        Assert.NotEmpty(response.Conflicts);
        Assert.Contains(response.ClarifyingQuestions, x => x.Contains("conflicting memories", StringComparison.OrdinalIgnoreCase));
        Assert.All(response.Conflicts, conflict =>
        {
            var existing = response.RelevantMemories.Single(x => x.Id == conflict.ExistingMemoryId);
            var current = response.RelevantMemories.Single(x => x.Id == conflict.NewMemoryId);
            Assert.Equal(existing.Subject, current.Subject);
        });
    }

    [Fact]
    public async Task GetTaskContext_does_not_invent_cross_subject_conflicts()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMemoryService>();
        await SeedStandardMemoriesAsync(service, includeMvpWarning: false);

        var response = await service.GetTaskContextAsync(new TaskContextRequest(
            new TaskDescription(
                "Validate MCP retrieval quality",
                "Check storage, retrieval, launch configuration, and MCP tool context quality in one planning task.",
                "validation"),
            new WorkArea("MCP retrieval validation", "technical-area", Modules: ["MBank.Api", "MCP"]),
            Limit: 5), CancellationToken.None);

        Assert.True(response.RelevantMemories.Select(x => x.Subject).Distinct().Count() > 1);
        Assert.Empty(response.Conflicts);
        Assert.DoesNotContain(response.ClarifyingQuestions, x => x.Contains("conflicting memories", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Get_returns_source_evidence_for_stored_memory()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IMemoryService>();
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

    private static async Task SeedStandardMemoriesAsync(IMemoryService service, bool includeMvpWarning = true)
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
                "MBank MCP get_task_context was validated end to end with hybrid retrieval, RRF relevance reasons, and no noisy cross-subject conflicts.",
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

using MBank.Api.Auth;
using MBank.Api.Data;
using MBank.Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace MBank.Tests;

public sealed class StartTaskInstructionStatusServiceTests(MBankWebApplicationFactory factory)
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
    public async Task Reports_outdated_only_once_during_notification_cooldown()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StartTaskInstructionStatusService>();
        var authenticatedScope = GetAuthenticatedScope(scope.ServiceProvider);

        var firstStatus = await service.GetAsync(
            authenticatedScope,
            "0.9.0",
            CancellationToken.None);
        var secondStatus = await service.GetAsync(
            authenticatedScope,
            "0.9.0",
            CancellationToken.None);

        Assert.Equal("outdated", firstStatus.Status);
        Assert.Equal("current", secondStatus.Status);
        Assert.Equal("0.9.0", secondStatus.ReportedVersion);
        Assert.Empty(secondStatus.Warnings);
    }

    [Fact]
    public async Task Reports_outdated_again_after_notification_cooldown()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StartTaskInstructionStatusService>();
        var authenticatedScope = GetAuthenticatedScope(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
        var timeProvider = scope.ServiceProvider.GetRequiredService<TimeProvider>();

        await service.GetAsync(authenticatedScope, "0.9.0", CancellationToken.None);

        var membership = await db.ProjectMemberships.SingleAsync(x =>
            x.UserId == authenticatedScope.UserId
            && x.ProjectId == authenticatedScope.ProjectId);
        db.Entry(membership)
            .Property(x => x.LastInstructionUpdateNotificationAt)
            .CurrentValue = timeProvider.GetUtcNow().AddDays(-8);
        await db.SaveChangesAsync();

        var status = await service.GetAsync(
            authenticatedScope,
            "0.9.0",
            CancellationToken.None);

        Assert.Equal("outdated", status.Status);
    }

    [Fact]
    public async Task Fails_when_authenticated_scope_has_no_project_membership()
    {
        using var scope = factory.Services.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<StartTaskInstructionStatusService>();
        var authenticatedScope = GetAuthenticatedScope(scope.ServiceProvider);
        var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();

        await db.ProjectMemberships
            .Where(x => x.UserId == authenticatedScope.UserId && x.ProjectId == authenticatedScope.ProjectId)
            .ExecuteDeleteAsync();

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            service.GetAsync(authenticatedScope, "0.9.0", CancellationToken.None));

        Assert.Contains("not a member", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    private static AuthenticatedMemoryScope GetAuthenticatedScope(IServiceProvider services)
    {
        return services.GetRequiredService<MemoryScopeProvider>().Current
            ?? throw new InvalidOperationException("The test memory scope is not configured.");
    }
}

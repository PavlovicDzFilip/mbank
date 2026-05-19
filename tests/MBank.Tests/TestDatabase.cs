using MBank.Api.Data;
using MBank.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace MBank.Tests;

internal sealed class TestDatabase : IAsyncDisposable
{
    public static readonly UserId UserId = new(Guid.Parse("11111111-1111-7111-8111-111111111111"));
    public static readonly ProjectId ProjectId = new(Guid.Parse("22222222-2222-7222-8222-222222222222"));
    public const string ProjectToken = "mbp_test_project_token";
    public const string AdminToken = "test-admin-token";

    private readonly PostgreSqlContainer _container;

    private TestDatabase(PostgreSqlContainer container)
    {
        _container = container;
        ConnectionString = container.GetConnectionString();
    }

    public string ConnectionString { get; }

    public static async Task<TestDatabase> CreateAsync()
    {
        var container = new PostgreSqlBuilder("pgvector/pgvector:pg16")
            .WithDatabase("mbank_tests")
            .WithUsername("mbank")
            .WithPassword("mbank")
            .Build();

        await container.StartAsync();

        var database = new TestDatabase(container);
        await using var context = database.CreateDbContext();
        await context.Database.MigrateAsync();
        return database;
    }

    public MemoryDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<MemoryDbContext>()
            .UseNpgsql(ConnectionString, npgsql => npgsql.UseVector())
            .Options;

        return new MemoryDbContext(options);
    }

    public async Task ResetAsync()
    {
        await using var context = CreateDbContext();
        await context.Database.ExecuteSqlRawAsync("DELETE FROM memory_links");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM memories");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM source_records");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM access_tokens");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM project_memberships");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM projects");
        await context.Database.ExecuteSqlRawAsync("DELETE FROM users");
        context.Users.Add(UserAccount.Create("Test User", "test@example.test", UserId));
        context.Projects.Add(Project.Create("Test Project", ProjectId));
        context.ProjectMemberships.Add(ProjectMembership.Create(UserId, ProjectId));
        var issuedToken = AccessToken.Create(
            UserId,
            ProjectId,
            "Test token",
            AccessTokenPermissions.Crud,
            ProjectToken);
        context.AccessTokens.Add(issuedToken.AccessToken);
        await context.SaveChangesAsync();
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

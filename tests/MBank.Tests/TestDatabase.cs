using MBank.Api.Data;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;
using Testcontainers.PostgreSql;

namespace MBank.Tests;

internal sealed class TestDatabase : IAsyncDisposable
{
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
    }

    public async ValueTask DisposeAsync()
    {
        await _container.DisposeAsync();
    }
}

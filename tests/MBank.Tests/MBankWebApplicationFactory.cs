using MBank.Api.Auth;
using MBank.Api.Data;
using MBank.Api.Domain;
using MBank.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MBank.Tests;

public sealed class MBankWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private TestDatabase? _database;

    public async Task InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }
    }

    public async Task ResetDatabaseAsync()
    {
        if (_database is null)
        {
            throw new InvalidOperationException("Test database has not been initialized.");
        }

        await _database.ResetAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:MemoryDb"] = RequireDatabase().ConnectionString,
                ["OpenAI:ApiKey"] = "test-api-key",
                ["OpenAI:EmbeddingModel"] = "test-embedding-model",
                ["OpenAI:EmbeddingDimensions"] = "1536",
                ["MBANK_ADMIN_TOKEN"] = TestDatabase.AdminToken
            });
        });

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<MemoryDbContext>>();
            services.RemoveAll<IEmbeddingService>();
            services.RemoveAll<MemoryScopeProvider>();

            services.AddDbContext<MemoryDbContext>(options =>
                options.UseNpgsql(RequireDatabase().ConnectionString, npgsql => npgsql.UseVector()));

            services.AddScoped<IEmbeddingService, FakeEmbeddingService>();
            services.AddScoped(_ =>
            {
                var currentScope = new MemoryScopeProvider();
                currentScope.Set(new AuthenticatedMemoryScope(
                    TestDatabase.UserId,
                    TestDatabase.ProjectId,
                    AccessTokenPermissions.Crud));
                return currentScope;
            });
        });
    }

    private TestDatabase RequireDatabase()
    {
        return _database ?? throw new InvalidOperationException("Test database has not been initialized.");
    }
}

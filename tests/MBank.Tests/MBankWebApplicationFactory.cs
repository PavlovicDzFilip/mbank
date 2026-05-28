using MBank.Api.Auth;
using MBank.Api.Data;
using MBank.Api.Domain;
using MBank.Api.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace MBank.Tests;

public sealed class MBankWebApplicationFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private TestDatabase? _database;
    private readonly Dictionary<string, string?> _originalEnvironment = [];

    public async Task InitializeAsync()
    {
        _database = await TestDatabase.CreateAsync();
        SetTestEnvironment("ConnectionStrings__MemoryDb", _database.ConnectionString);
        SetTestEnvironment("OpenAI__ApiKey", "test-api-key");
        SetTestEnvironment("OpenAI__EmbeddingModel", "test-embedding-model");
        SetTestEnvironment("OpenAI__EmbeddingDimensions", "1536");
        SetTestEnvironment("Smtp__Host", "localhost");
        SetTestEnvironment("Smtp__Port", "1025");
        SetTestEnvironment("Smtp__FromAddress", "noreply@example.test");
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();
        if (_database is not null)
        {
            await _database.DisposeAsync();
        }

        foreach (var (name, value) in _originalEnvironment)
        {
            Environment.SetEnvironmentVariable(name, value);
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
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<MemoryDbContext>>();
            services.RemoveAll<IEmbeddingService>();
            services.RemoveAll<IEmailSender>();
            services.RemoveAll<MemoryScopeProvider>();

            services.AddDbContext<MemoryDbContext>(options =>
                options.UseNpgsql(RequireDatabase().ConnectionString, npgsql => npgsql.UseVector()));

            services.AddScoped<IEmbeddingService, FakeEmbeddingService>();
            services.AddSingleton<FakeEmailSender>();
            services.AddSingleton<IEmailSender>(provider => provider.GetRequiredService<FakeEmailSender>());
            services.AddScoped(_ =>
            {
                var currentScope = new MemoryScopeProvider();
                currentScope.Set(new AuthenticatedMemoryScope(
                    RequireDatabase().UserId,
                    TestDatabase.ProjectId,
                    AccessTokenPermissions.Crud));
                return currentScope;
            });
        });
    }

    public UserId DefaultUserId => RequireDatabase().UserId;

    private TestDatabase RequireDatabase()
    {
        return _database ?? throw new InvalidOperationException("Test database has not been initialized.");
    }

    private void SetTestEnvironment(string name, string value)
    {
        if (!_originalEnvironment.ContainsKey(name))
        {
            _originalEnvironment[name] = Environment.GetEnvironmentVariable(name);
        }

        Environment.SetEnvironmentVariable(name, value);
    }
}

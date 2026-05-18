using MBank.Api.Data;
using MBank.MigrationService.CodeMigrations.Engine;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Pgvector;

var builder = Host.CreateApplicationBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("MemoryDb")
                       ?? throw new InvalidOperationException("ConnectionStrings:MemoryDb is required.");

builder.Services.AddSingleton(_ =>
{
    var dataSourceBuilder = new NpgsqlDataSourceBuilder(connectionString);
    dataSourceBuilder.UseVector();
    return dataSourceBuilder.Build();
});

builder.Services.AddDbContext<MemoryDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlDataSource>(), npgsql => npgsql.UseVector()));

builder.Services.AddDbContext<CodeMigrationDbContext>((services, options) =>
    options.UseNpgsql(services.GetRequiredService<NpgsqlDataSource>()));

builder.Services.AddScoped<CodeMigrationRunner>();

var app = builder.Build();
await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();
    var loggerFactory = scope.ServiceProvider.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger(nameof(MBank));
    logger.LogInformation("Applying MBank database migrations.");
    await db.Database.MigrateAsync();
    logger.LogInformation("MBank database migrations are up to date.");
}

await using (var scope = app.Services.CreateAsyncScope())
{
    var db = scope.ServiceProvider.GetRequiredService<CodeMigrationDbContext>();
    await db.Database.MigrateAsync();
    var codeMigrationRunner = scope.ServiceProvider.GetRequiredService<CodeMigrationRunner>();
    await codeMigrationRunner.RunAsync(CancellationToken.None);
}

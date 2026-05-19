using System.Reflection;
using MBank.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace MBank.MigrationService.CodeMigrations.Engine;

internal sealed class CodeMigrationRunner(
    MemoryDbContext db,
    IServiceScopeFactory serviceScopeFactory,
    ILogger<CodeMigrationRunner> logger)
{
    private readonly Assembly _migrationAssembly = typeof(CodeMigrationRunner).Assembly;

    internal CodeMigrationRunner(
        MemoryDbContext db,
        IServiceScopeFactory serviceScopeFactory,
        ILogger<CodeMigrationRunner> logger,
        Assembly migrationAssembly)
        : this(db, serviceScopeFactory, logger)
    {
        _migrationAssembly = migrationAssembly;
    }

    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var migrations = DiscoverMigrations(_migrationAssembly);
        if (migrations.Count == 0)
        {
            logger.LogInformation("No MBank code migrations were discovered.");
            return;
        }

        var appliedMigrations =
            await db.CodeMigrations
                .OrderBy(x => x.Id)
                .ToListAsync(cancellationToken: cancellationToken);

        var appliedMigrationIds = appliedMigrations.Select(x => x.Id).ToHashSet();

        foreach (var migration in migrations)
        {
            if (appliedMigrationIds.Contains(migration.Id))
            {
                logger.LogInformation("Skipping already applied MBank code migration {MigrationId}.", migration.Id);
                continue;
            }

            await RunMigrationAsync(migration, cancellationToken);
            appliedMigrationIds.Add(migration.Id);
        }
    }

    private static IReadOnlyList<CodeMigrationDescriptor> DiscoverMigrations(Assembly assembly)
    {
        var migrations = assembly.GetTypes()
            .Where(type => type is { IsClass: true, IsAbstract: false })
            .Where(type => typeof(ICodeMigration).IsAssignableFrom(type))
            .Select(CodeMigrationDescriptor.Create)
            .OrderBy(x => x.Id)
            .ToList();

        return migrations;
    }

    private async Task RunMigrationAsync(
        CodeMigrationDescriptor migration,
        CancellationToken cancellationToken)
    {
        logger.LogInformation(
            "Applying MBank code migration {MigrationId} ({MigrationType}).",
            migration.Id,
            migration.Type.Name);

        await using var scope = serviceScopeFactory.CreateAsyncScope();
        var migrationDb = scope.ServiceProvider.GetRequiredService<MemoryDbContext>();

        await using var transaction =
            await migrationDb.Database.BeginTransactionAsync(cancellationToken);

        var alreadyApplied = await migrationDb.CodeMigrations
            .AnyAsync(x => x.Id == migration.Id, cancellationToken);

        if (alreadyApplied)
        {
            logger.LogInformation(
                "Skipping MBank code migration {MigrationId}; already applied.",
                migration.Id);

            return;
        }

        var migrationInstance =
            (ICodeMigration)ActivatorUtilities.CreateInstance(
                scope.ServiceProvider,
                migration.Type);

        await migrationInstance.RunAsync();

        migrationDb.CodeMigrations.Add(CodeMigration.Create(migration.Id));

        await migrationDb.SaveChangesAsync(cancellationToken);

        await transaction.CommitAsync(cancellationToken);

        logger.LogInformation("Applied MBank code migration {MigrationId}.", migration.Id);
    }
}

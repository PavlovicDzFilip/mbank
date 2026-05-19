extern alias MigrationService;
using MBank.Api.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MigrationService::MBank.MigrationService.CodeMigrations.Engine;
using Testcontainers.PostgreSql;

namespace MBank.Tests;

public sealed class CodeMigrationEngineTests
{
    [Fact]
    public void Descriptor_create_reads_migration_attribute()
    {
        var descriptor = CodeMigrationDescriptor.Create(typeof(ValidCodeMigration));

        Assert.Equal("20260517120000_valid_code_migration", descriptor.Id);
        Assert.Equal(typeof(ValidCodeMigration), descriptor.Type);
    }

    [Fact]
    public void Descriptor_create_rejects_missing_migration_attribute()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => CodeMigrationDescriptor.Create(typeof(object)));

        Assert.Contains(nameof(CodeMigrationAttribute), exception.Message);
    }

    [Fact]
    public void Descriptor_create_rejects_invalid_migration_id()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => CodeMigrationDescriptor.Create(typeof(InvalidIdCodeMigration)));

        Assert.Contains("Invalid migration ID format", exception.Message);
    }

    [Fact]
    public void CodeMigration_create_copies_id_and_uses_utc_timestamp()
    {
        var descriptor = CodeMigrationDescriptor.Create(typeof(ValidCodeMigration));
        var before = DateTime.UtcNow;

        var migration = CodeMigration.Create(descriptor.Id);

        Assert.Equal(descriptor.Id, migration.Id);
        Assert.Equal(DateTimeKind.Utc, migration.AppliedAt.Kind);
        Assert.InRange(migration.AppliedAt, before, DateTime.UtcNow);
    }

    [Fact]
    public async Task DbContext_maps_and_persists_code_migration_ledger()
    {
        await using var harness = await CodeMigrationHarness.CreateAsync();

        await harness.RecordAppliedMigrationAsync<ValidCodeMigration>();

        await using var db = harness.CreateDbContext();
        var entity = db.Model.FindEntityType(typeof(CodeMigration));
        var storedMigration = await db.CodeMigrations.SingleAsync();

        Assert.NotNull(entity);
        Assert.Equal("__code_migrations", entity.GetTableName());
        Assert.Equal(nameof(CodeMigration.Id), entity.FindPrimaryKey()?.Properties.Single().Name);
        Assert.Equal(300, entity.FindProperty(nameof(CodeMigration.Id))?.GetMaxLength());
        Assert.False(entity.FindProperty(nameof(CodeMigration.Id))?.IsNullable);
        Assert.False(entity.FindProperty(nameof(CodeMigration.AppliedAt))?.IsNullable);
        Assert.Equal(MigrationId<ValidCodeMigration>(), storedMigration.Id);
    }

    [Fact]
    public async Task Runner_runs_pending_migration()
    {
        await using var harness = await CodeMigrationHarness.CreateAsync();

        await harness.RunAsync();

        Assert.Contains("recording", harness.Runs);
        Assert.True(await harness.WasAppliedAsync<RecordingCodeMigration>());
    }

    [Fact]
    public async Task Runner_skips_already_applied_migration()
    {
        await using var harness = await CodeMigrationHarness.CreateAsync();
        await harness.RecordAppliedMigrationAsync<RecordingCodeMigration>();

        await harness.RunAsync();

        Assert.DoesNotContain("recording", harness.Runs);
        Assert.Equal(1, await harness.AppliedCountAsync<RecordingCodeMigration>());
    }

    [Fact]
    public async Task Runner_runs_multiple_migrations_in_id_order()
    {
        await using var harness = await CodeMigrationHarness.CreateAsync();

        await harness.RunAsync();

        Assert.Equal(
            ["ordered_first", "ordered_second", "ordered_third"],
            harness.Runs.Where(run => run.StartsWith("ordered_", StringComparison.Ordinal)));
    }

    [Fact]
    public async Task Runner_creates_new_scope_for_each_pending_migration()
    {
        await using var harness = await CodeMigrationHarness.CreateAsync();

        await harness.RunAsync();

        var scopeRuns = harness.Runs
            .Where(run => run.StartsWith("scope_", StringComparison.Ordinal))
            .Select(run => Guid.Parse(run.Split(':')[1]))
            .ToArray();

        Assert.Equal(2, scopeRuns.Length);
        Assert.NotEqual(scopeRuns[0], scopeRuns[1]);
    }

    private static string MigrationId<TMigration>()
    {
        return CodeMigrationDescriptor.Create(typeof(TMigration)).Id;
    }

    [CodeMigration("20260517120000_valid_code_migration")]
    private sealed class ValidCodeMigration : ICodeMigration
    {
        public Task RunAsync() => Task.CompletedTask;
    }

    [CodeMigration("bad-id")]
    private sealed class InvalidIdCodeMigration;

    [CodeMigration("20260517120100_recording_code_migration")]
    private sealed class RecordingCodeMigration(MigrationRunRecorder recorder) : RecordingMigration(recorder, "recording");

    [CodeMigration("20260517120202_ordered_third")]
    private sealed class OrderedThirdCodeMigration(MigrationRunRecorder recorder) : RecordingMigration(recorder, "ordered_third");

    [CodeMigration("20260517120200_ordered_first")]
    private sealed class OrderedFirstCodeMigration(MigrationRunRecorder recorder) : RecordingMigration(recorder, "ordered_first");

    [CodeMigration("20260517120201_ordered_second")]
    private sealed class OrderedSecondCodeMigration(MigrationRunRecorder recorder) : RecordingMigration(recorder, "ordered_second");

    [CodeMigration("20260517120300_scoped_first")]
    private sealed class ScopedFirstCodeMigration(MigrationRunRecorder recorder, ScopedMigrationMarker marker)
        : ScopedMigration(recorder, marker, "scope_first");

    [CodeMigration("20260517120301_scoped_second")]
    private sealed class ScopedSecondCodeMigration(MigrationRunRecorder recorder, ScopedMigrationMarker marker)
        : ScopedMigration(recorder, marker, "scope_second");

    private abstract class RecordingMigration(MigrationRunRecorder recorder, string name) : ICodeMigration
    {
        public Task RunAsync()
        {
            recorder.Record(name);
            return Task.CompletedTask;
        }
    }

    private abstract class ScopedMigration(
        MigrationRunRecorder recorder,
        ScopedMigrationMarker marker,
        string name) : ICodeMigration
    {
        public Task RunAsync()
        {
            recorder.Record($"{name}:{marker.Id}");
            return Task.CompletedTask;
        }
    }

    private sealed class ScopedMigrationMarker
    {
        public Guid Id { get; } = Guid.NewGuid();
    }

    private sealed class MigrationRunRecorder
    {
        private readonly List<string> _runs = [];

        public IReadOnlyList<string> Runs => _runs;

        public void Record(string name)
        {
            _runs.Add(name);
        }
    }

    private sealed class CodeMigrationHarness : IAsyncDisposable
    {
        private const string DatabaseName = "mbank_tests";
        private const string Username = "mbank";
        private const string Password = "mbank";

        private readonly PostgreSqlContainer _container;
        private readonly ServiceProvider _serviceProvider;
        private readonly AsyncServiceScope _runnerScope;
        private readonly CodeMigrationRunner _runner;
        private readonly DbContextOptions<MemoryDbContext> _dbOptions;

        private CodeMigrationHarness(
            PostgreSqlContainer container,
            ServiceProvider serviceProvider,
            AsyncServiceScope runnerScope,
            CodeMigrationRunner runner,
            MigrationRunRecorder recorder,
            DbContextOptions<MemoryDbContext> dbOptions)
        {
            _container = container;
            _serviceProvider = serviceProvider;
            _runnerScope = runnerScope;
            _runner = runner;
            _dbOptions = dbOptions;
            Recorder = recorder;
        }

        public IReadOnlyList<string> Runs => Recorder.Runs;

        private MigrationRunRecorder Recorder { get; }

        public static async Task<CodeMigrationHarness> CreateAsync()
        {
            var container = CreatePostgresContainer();
            await container.StartAsync();

            var recorder = new MigrationRunRecorder();
            var connectionString = container.GetConnectionString();
            var serviceProvider = BuildServiceProvider(connectionString, recorder);
            var dbOptions = CreateDbOptions(connectionString);

            await EnsureDatabaseExistsAsync(dbOptions);

            var runnerScope = serviceProvider.CreateAsyncScope();
            var runner = new CodeMigrationRunner(
                runnerScope.ServiceProvider.GetRequiredService<MemoryDbContext>(),
                serviceProvider.GetRequiredService<IServiceScopeFactory>(),
                runnerScope.ServiceProvider.GetRequiredService<ILogger<CodeMigrationRunner>>(),
                typeof(CodeMigrationEngineTests).Assembly);

            return new CodeMigrationHarness(
                container,
                serviceProvider,
                runnerScope,
                runner,
                recorder,
                dbOptions);
        }

        public MemoryDbContext CreateDbContext()
        {
            return new MemoryDbContext(_dbOptions);
        }

        public Task RunAsync()
        {
            return _runner.RunAsync(CancellationToken.None);
        }

        public async Task RecordAppliedMigrationAsync<TMigration>()
        {
            await using var db = CreateDbContext();
            db.CodeMigrations.Add(CodeMigration.Create(CodeMigrationDescriptor.Create(typeof(TMigration)).Id));
            await db.SaveChangesAsync();
        }

        public async Task<bool> WasAppliedAsync<TMigration>()
        {
            await using var db = CreateDbContext();
            var migrationId = MigrationId<TMigration>();
            return await db.CodeMigrations.AnyAsync(migration => migration.Id == migrationId);
        }

        public async Task<int> AppliedCountAsync<TMigration>()
        {
            await using var db = CreateDbContext();
            var migrationId = MigrationId<TMigration>();
            return await db.CodeMigrations.CountAsync(migration => migration.Id == migrationId);
        }

        public async ValueTask DisposeAsync()
        {
            await _runnerScope.DisposeAsync();
            await _serviceProvider.DisposeAsync();
            await _container.DisposeAsync();
        }

        private static PostgreSqlContainer CreatePostgresContainer()
        {
            return new PostgreSqlBuilder("pgvector/pgvector:pg16")
                .WithDatabase(DatabaseName)
                .WithUsername(Username)
                .WithPassword(Password)
                .Build();
        }

        private static ServiceProvider BuildServiceProvider(
            string connectionString,
            MigrationRunRecorder recorder)
        {
            var services = new ServiceCollection();
            services.AddSingleton(recorder);
            services.AddScoped<ScopedMigrationMarker>();
            services.AddLogging();
            services.AddDbContext<MemoryDbContext>(options =>
                options.UseNpgsql(connectionString, npgsql => npgsql.UseVector()));

            return services.BuildServiceProvider(validateScopes: true);
        }

        private static DbContextOptions<MemoryDbContext> CreateDbOptions(string connectionString)
        {
            return new DbContextOptionsBuilder<MemoryDbContext>()
                .UseNpgsql(connectionString, npgsql => npgsql.UseVector())
                .Options;
        }

        private static async Task EnsureDatabaseExistsAsync(
            DbContextOptions<MemoryDbContext> options)
        {
            await using var db = new MemoryDbContext(options);
            await db.Database.EnsureCreatedAsync();
        }
    }
}

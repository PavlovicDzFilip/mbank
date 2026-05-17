using Microsoft.EntityFrameworkCore;

namespace MBank.MigrationService.CodeMigrations.Engine;

public sealed class CodeMigrationDbContext(DbContextOptions<CodeMigrationDbContext> options) : DbContext(options)
{
    public DbSet<CodeMigration> CodeMigrations => Set<CodeMigration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<CodeMigration>(entity =>
        {
            entity.ToTable("__code_migrations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .HasMaxLength(300)
                .IsRequired();

            entity.Property(x => x.AppliedAt).IsRequired();
        });
    }
}
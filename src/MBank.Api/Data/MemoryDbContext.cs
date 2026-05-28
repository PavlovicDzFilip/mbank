using MBank.Api.Domain;
using Microsoft.EntityFrameworkCore;

namespace MBank.Api.Data;

public sealed class MemoryDbContext(DbContextOptions<MemoryDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectOpenAiCredential> ProjectOpenAiCredentials => Set<ProjectOpenAiCredential>();

    public DbSet<ProjectMembership> ProjectMemberships => Set<ProjectMembership>();

    public DbSet<AccessToken> AccessTokens => Set<AccessToken>();

    public DbSet<MagicLinkLogin> MagicLinkLogins => Set<MagicLinkLogin>();

    public DbSet<MemoryEntry> Memories => Set<MemoryEntry>();

    public DbSet<SourceRecord> SourceRecords => Set<SourceRecord>();

    public DbSet<MemoryLink> MemoryLinks => Set<MemoryLink>();

    public DbSet<CodeMigration> CodeMigrations => Set<CodeMigration>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .HasConversion(id => id.Value, value => new UserId(value));
            entity.Property(x => x.Name)
                .HasMaxLength(256);
            entity.Property(x => x.Email)
                .IsRequired()
                .HasMaxLength(320);
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .HasConversion(id => id.Value, value => new ProjectId(value));
            entity.Property(x => x.Name)
                .HasMaxLength(256);
        });

        modelBuilder.Entity<ProjectOpenAiCredential>(entity =>
        {
            entity.ToTable("project_openai_credentials");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .HasConversion(id => id.Value, value => new ProjectOpenAiCredentialId(value));
            entity.Property(x => x.ProjectId)
                .HasConversion(id => id.Value, value => new ProjectId(value));
            entity.Property(x => x.EncryptedApiKey)
                .IsRequired()
                .HasMaxLength(4096);
            entity.Property(x => x.ApiKeySuffix)
                .IsRequired()
                .HasMaxLength(16);
            entity.HasIndex(x => x.ProjectId).IsUnique();
            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ProjectMembership>(entity =>
        {
            entity.ToTable("project_memberships");
            entity.HasKey(x => new { x.UserId, x.ProjectId });
            entity.Property(x => x.UserId)
                .HasConversion(id => id.Value, value => new UserId(value));
            entity.Property(x => x.ProjectId)
                .HasConversion(id => id.Value, value => new ProjectId(value));
            entity.HasIndex(x => x.ProjectId);
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AccessToken>(entity =>
        {
            entity.ToTable("access_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .HasConversion(id => id.Value, value => new AccessTokenId(value));
            entity.Property(x => x.UserId)
                .HasConversion(id => id.Value, value => new UserId(value));
            entity.Property(x => x.ProjectId)
                .HasConversion(id => id.Value, value => new ProjectId(value));
            entity.Property(x => x.Name)
                .HasMaxLength(256);
            entity.Property(x => x.TokenHash)
                .HasMaxLength(128);
            entity.Property(x => x.TokenPrefix)
                .HasMaxLength(16);
            entity.Property(x => x.TokenSuffix)
                .HasMaxLength(16);
            entity.Property(x => x.Permissions).HasConversion<string>().HasMaxLength(64);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.ProjectId, x.Name }).IsUnique();
            entity.HasOne<ProjectMembership>()
                .WithMany()
                .HasForeignKey(x => new { x.UserId, x.ProjectId })
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MagicLinkLogin>(entity =>
        {
            entity.ToTable("magic_link_logins");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .HasConversion(id => id.Value, value => new MagicLinkLoginId(value));
            entity.Property(x => x.Email)
                .HasMaxLength(320);
            entity.Property(x => x.TokenHash)
                .HasMaxLength(128);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => x.Email);
            entity.HasIndex(x => x.ExpiresAt);
        });

        modelBuilder.Entity<MemoryEntry>(entity =>
        {
            entity.ToTable("memories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .HasConversion(id => id.Value, value => new MemoryId(value));
            entity.Property(x => x.Type)
                .HasConversion(type => type.Value, value => MemoryType.FromValue(value))
                .HasMaxLength(64);
            entity.Property(x => x.Summary)
                .HasMaxLength(2000);
            entity.Property(x => x.Subject)
                .HasMaxLength(256);
            entity.Property(x => x.Status)
                .HasMaxLength(32);
            entity.Property(x => x.UserId)
                .HasConversion(id => id.Value, value => new UserId(value));
            entity.Property(x => x.ProjectId)
                .HasConversion(id => id.Value, value => new ProjectId(value));
            entity.Property(x => x.SearchText)
                .HasMaxLength(5000);
            entity.Property(x => x.SourceRecordId)
                .HasConversion(
                    sourceRecordId => sourceRecordId.HasValue ? sourceRecordId.Value.Value : (Guid?)null,
                    value => value.HasValue ? new SourceRecordId(value.Value) : null);
            entity.HasGeneratedTsVectorColumn(
                x => x.SearchVector,
                "english",
                x => new { x.SearchText })
                .HasIndex(x => x.SearchVector)
                .HasMethod("GIN");
            entity.HasIndex(x => x.Type);
            entity.HasIndex(x => x.UserId);
            entity.HasIndex(x => x.ProjectId);
            entity.HasIndex(x => x.Subject);
            entity.HasIndex(x => x.Status);
            entity.HasIndex(x => x.Embedding)
                .HasMethod("hnsw")
                .HasOperators("vector_cosine_ops")
                .HasStorageParameter("m", 16)
                .HasStorageParameter("ef_construction", 64);
            entity.HasOne<SourceRecord>()
                .WithMany()
                .HasForeignKey(x => x.SourceRecordId)
                .OnDelete(DeleteBehavior.SetNull);
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Restrict);
            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<SourceRecord>(entity =>
        {
            entity.ToTable("source_records");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .HasConversion(id => id.Value, value => new SourceRecordId(value));
            entity.Property(x => x.SourceKind).HasMaxLength(64);
            entity.Property(x => x.SourceUrl).HasMaxLength(2048);
            entity.Property(x => x.ConversationUrl).HasMaxLength(2048);
        });

        modelBuilder.Entity<MemoryLink>(entity =>
        {
            entity.ToTable("memory_links");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .HasConversion(id => id.Value, value => new MemoryLinkId(value));
            entity.Property(x => x.FromMemoryId)
                .HasConversion(id => id.Value, value => new MemoryId(value));
            entity.Property(x => x.ToMemoryId)
                .HasConversion(id => id.Value, value => new MemoryId(value));
            entity.Property(x => x.Relation)
                .HasMaxLength(64);
            entity.Property(x => x.Description).HasMaxLength(2000);
            entity.HasIndex(x => new { x.FromMemoryId, x.ToMemoryId }).IsUnique();
            entity.HasOne<MemoryEntry>()
                .WithMany()
                .HasForeignKey(x => x.FromMemoryId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<MemoryEntry>()
                .WithMany()
                .HasForeignKey(x => x.ToMemoryId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<CodeMigration>(entity =>
        {
            entity.ToTable("__code_migrations");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Id)
                .HasMaxLength(300)
                .IsRequired();
            entity.Property(x => x.AppliedAt)
                .IsRequired();
        });
    }
}

using MBank.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace MBank.Api.Data;

public sealed class MemoryDbContext(DbContextOptions<MemoryDbContext> options) : DbContext(options)
{
    public DbSet<UserAccount> Users => Set<UserAccount>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<AccessToken> AccessTokens => Set<AccessToken>();

    public DbSet<MemoryEntry> Memories => Set<MemoryEntry>();

    public DbSet<SourceRecord> SourceRecords => Set<SourceRecord>();

    public DbSet<MemoryLink> MemoryLinks => Set<MemoryLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

        modelBuilder.Entity<UserAccount>(entity =>
        {
            entity.ToTable("users");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(256);
            entity.Property(x => x.Email).HasMaxLength(320);
            entity.HasIndex(x => x.Email).IsUnique();
        });

        modelBuilder.Entity<Project>(entity =>
        {
            entity.ToTable("projects");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(256);
            entity.HasIndex(x => x.Name).IsUnique();
        });

        modelBuilder.Entity<AccessToken>(entity =>
        {
            entity.ToTable("access_tokens");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Name).HasMaxLength(256);
            entity.Property(x => x.TokenHash).HasMaxLength(128);
            entity.Property(x => x.Permissions).HasConversion<string>().HasMaxLength(64);
            entity.HasIndex(x => x.TokenHash).IsUnique();
            entity.HasIndex(x => new { x.UserId, x.ProjectId, x.Name }).IsUnique();
            entity.HasOne<UserAccount>()
                .WithMany()
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
            entity.HasOne<Project>()
                .WithMany()
                .HasForeignKey(x => x.ProjectId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MemoryEntry>(entity =>
        {
            entity.ToTable("memories");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Type).HasMaxLength(64);
            entity.Property(x => x.Summary).HasMaxLength(2000);
            entity.Property(x => x.Subject).HasMaxLength(256);
            entity.Property(x => x.SubjectKind).HasMaxLength(64);
            entity.Property(x => x.Status).HasMaxLength(32);
            entity.Property(x => x.SearchText).HasMaxLength(5000);
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
            entity.HasIndex(x => x.SubjectKind);
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
            entity.Property(x => x.SourceKind).HasMaxLength(64);
            entity.Property(x => x.SourceUrl).HasMaxLength(2048);
            entity.Property(x => x.ConversationUrl).HasMaxLength(2048);
        });

        modelBuilder.Entity<MemoryLink>(entity =>
        {
            entity.ToTable("memory_links");
            entity.HasKey(x => x.Id);
            entity.Property(x => x.Relation).HasMaxLength(64);
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
    }
}

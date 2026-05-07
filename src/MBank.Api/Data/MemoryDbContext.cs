using MBank.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Pgvector.EntityFrameworkCore;

namespace MBank.Api.Data;

public sealed class MemoryDbContext(DbContextOptions<MemoryDbContext> options) : DbContext(options)
{
    public DbSet<MemoryEntry> Memories => Set<MemoryEntry>();

    public DbSet<SourceRecord> SourceRecords => Set<SourceRecord>();

    public DbSet<MemoryLink> MemoryLinks => Set<MemoryLink>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("vector");

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

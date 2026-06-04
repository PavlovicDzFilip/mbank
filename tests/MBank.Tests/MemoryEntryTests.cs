using MBank.Api.Domain;
using MBank.Api.Services;
using Pgvector;

namespace MBank.Tests;

public sealed class MemoryEntryTests
{
    [Fact]
    public async Task Create_derives_search_text_from_memory_inputs()
    {
        string? embeddedText = null;
        var embedding = new Vector(new float[1536]);
        var embeddings = new RecordingEmbeddingService(searchText =>
        {
            embeddedText = searchText;
            return embedding;
        });

        var entry = await MemoryEntry.CreateAsync(
            UserId.New(),
            ProjectId.New(),
            MemoryType.TechnicalConvention,
            "  Search text belongs inside memory creation.  ",
            "  Domain factory  ",
            [" factory-owned ", "Factory-Owned", "search-text"],
            0.8,
            new string('x', 1300),
            null,
            MemoryEntry.ActiveStatus,
            embeddings,
            CancellationToken.None);

        Assert.Contains("Type: technical-convention", entry.SearchText);
        Assert.Contains("Subject: Domain factory", entry.SearchText);
        Assert.Contains("Summary: Search text belongs inside memory creation.", entry.SearchText);
        Assert.Contains("Tags: factory-owned, search-text", entry.SearchText);
        Assert.Contains($"Source excerpt: {new string('x', 1200)}", entry.SearchText);
        Assert.DoesNotContain(new string('x', 1201), entry.SearchText);
        Assert.Equal(entry.SearchText, embeddedText);
        Assert.Same(embedding, entry.Embedding);
    }

    [Fact]
    public async Task Create_requires_embedding_factory()
    {
        await Assert.ThrowsAsync<ArgumentNullException>(() => MemoryEntry.CreateAsync(
            UserId.New(),
            ProjectId.New(),
            MemoryType.TechnicalConvention,
            "Embedding is generated from the created search text.",
            "Domain factory",
            ["embedding"],
            0.8,
            null,
            null,
            MemoryEntry.ActiveStatus,
            embeddings: null!,
            CancellationToken.None));
    }

    [Fact]
    public async Task Experimental_memory_can_be_promoted_to_active()
    {
        var embeddings = new RecordingEmbeddingService(_ => new Vector(new float[1536]));

        var entry = await MemoryEntry.CreateAsync(
            UserId.New(),
            ProjectId.New(),
            MemoryType.ProductReasoning,
            "Try a provisional product direction without making it authoritative.",
            "Experimental lifecycle",
            ["experiment"],
            0.7,
            null,
            null,
            MemoryEntry.ExperimentalStatus,
            embeddings,
            CancellationToken.None);

        Assert.Equal(MemoryEntry.ExperimentalStatus, entry.Status);

        entry.Promote();

        Assert.Equal(MemoryEntry.ActiveStatus, entry.Status);
    }

    private sealed class RecordingEmbeddingService(Func<string, Vector> generateEmbedding) : IEmbeddingService
    {
        public Task<Vector> GenerateEmbeddingAsync(ProjectId projectId, string text, CancellationToken cancellationToken)
        {
            return Task.FromResult(generateEmbedding(text));
        }
    }
}

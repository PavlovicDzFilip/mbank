using MBank.Api.Services;
using MBank.Api.Domain;
using Pgvector;

namespace MBank.Tests;

internal sealed class FakeEmbeddingService : IEmbeddingService
{
    private const int Dimensions = 1536;

    private static readonly (string Token, int Dimension, float Weight)[] Vocabulary =
    [
        ("mcp", 0, 1.0f),
        ("memory", 0, 0.9f),
        ("storage", 0, 1.0f),
        ("retrieval", 0, 1.0f),
        ("context", 0, 0.8f),
        ("launch", 1, 1.0f),
        ("working directory", 1, 1.0f),
        ("user-secrets", 1, 1.0f),
        ("secrets", 1, 1.0f),
        ("visible", 1, 0.6f),
        ("ui", 2, 1.0f),
        ("razor", 2, 1.0f),
        ("dashboard", 2, 0.8f),
        ("spa", 2, 0.8f),
        ("search", 3, 1.0f),
        ("tool", 3, 0.8f),
        ("enterprise", 4, 1.0f),
        ("billing", 4, 1.0f),
        ("permissions", 4, 1.0f),
        ("warning", 5, 1.0f),
        ("conflict", 5, 1.0f),
        ("validation", 6, 1.0f),
        ("hybrid", 6, 1.0f),
        ("rrf", 6, 1.0f),
        ("full-text", 6, 1.0f),
        ("apricotvector", 7, 3.0f),
        ("source", 8, 1.0f),
        ("evidence", 8, 1.0f)
    ];

    public Task<Vector> GenerateEmbeddingAsync(ProjectId projectId, string text, CancellationToken cancellationToken)
    {
        var vector = new float[Dimensions];
        var normalized = text.ToLowerInvariant();

        foreach (var (token, dimension, weight) in Vocabulary)
        {
            if (normalized.Contains(token, StringComparison.Ordinal))
            {
                vector[dimension] += weight;
            }
        }

        if (vector.All(x => x == 0))
        {
            vector[Dimensions - 1] = 1;
        }

        var length = MathF.Sqrt(vector.Sum(x => x * x));
        for (var i = 0; i < vector.Length; i++)
        {
            vector[i] /= length;
        }

        return Task.FromResult(new Vector(vector));
    }
}

using Pgvector;

namespace MBank.Api.Services;

public interface IEmbeddingService
{
    Task<Vector> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken);
}


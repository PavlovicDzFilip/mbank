using Pgvector;
using MBank.Api.Domain;

namespace MBank.Api.Services;

public interface IEmbeddingService
{
    Task<Vector> GenerateEmbeddingAsync(ProjectId projectId, string text, CancellationToken cancellationToken);
}


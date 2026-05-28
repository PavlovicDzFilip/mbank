using MBank.Api.Domain;
using MBank.Api.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;
using Pgvector;

namespace MBank.Api.Services;

public sealed class OpenAiEmbeddingService(
    IOptions<OpenAiOptions> options,
    ProjectOpenAiCredentialService openAiCredentials) : IEmbeddingService
{
    private readonly OpenAiOptions _options = options.Value;

    public async Task<Vector> GenerateEmbeddingAsync(ProjectId projectId, string text, CancellationToken cancellationToken)
    {
        var apiKey = await openAiCredentials.ResolveApiKeyAsync(projectId, cancellationToken);
        var client = new EmbeddingClient(_options.EmbeddingModel, apiKey);
        var generator = client.AsIEmbeddingGenerator(_options.EmbeddingDimensions);
        var embeddings = await generator.GenerateAsync([text], cancellationToken: cancellationToken);
        return new Vector(embeddings[0].Vector.ToArray());
    }
}

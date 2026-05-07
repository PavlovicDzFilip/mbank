using MBank.Api.Options;
using Microsoft.Extensions.AI;
using Microsoft.Extensions.Options;
using OpenAI.Embeddings;

namespace MBank.Api.Services;

public sealed class OpenAiEmbeddingService(IOptions<OpenAiOptions> options) : IEmbeddingService
{
    private readonly OpenAiOptions _options = options.Value;

    public async Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken)
    {
        var client = new EmbeddingClient(_options.EmbeddingModel, _options.ApiKey);
        var generator = client.AsIEmbeddingGenerator(_options.EmbeddingDimensions);
        var embeddings = await generator.GenerateAsync([text], cancellationToken: cancellationToken);
        return embeddings[0].Vector.ToArray();
    }
}

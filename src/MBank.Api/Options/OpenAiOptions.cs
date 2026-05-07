namespace MBank.Api.Options;

public sealed class OpenAiOptions
{
    public const string SectionName = "OpenAI";

    public string ApiKey { get; set; } = "";

    public string EmbeddingModel { get; set; } = "text-embedding-3-small";

    public int EmbeddingDimensions { get; set; } = 1536;
}

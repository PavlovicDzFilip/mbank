namespace MBank.Api.Options;

public sealed class MagicLinkOptions
{
    public const string SectionName = "MagicLink";

    public string PublicBaseUrl { get; set; } = string.Empty;
}

namespace MBank.Api.Domain;

public sealed class SourceRecord
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string? SourceKind { get; set; }

    public string? SourceText { get; set; }

    public string? SourceUrl { get; set; }

    public string? ConversationUrl { get; set; }

    public string? MetadataJson { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}


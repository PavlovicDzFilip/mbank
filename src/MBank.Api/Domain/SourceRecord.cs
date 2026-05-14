using System.Diagnostics;

namespace MBank.Api.Domain;

public sealed class SourceRecord
{
    private SourceRecord()
    {
    }

    public SourceRecordId Id { get; private set; } = SourceRecordId.New();

    public string? SourceKind { get; private set; }

    public string? SourceText { get; private set; }

    public string? SourceUrl { get; private set; }

    public string? ConversationUrl { get; private set; }

    public string? MetadataJson { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public static SourceRecord Create(
        string? sourceKind,
        string? sourceText,
        string? sourceUrl,
        string? conversationUrl,
        string? metadataJson)
    {
        return new SourceRecord
        {
            SourceKind = NormalizeOptional(sourceKind),
            SourceText = NormalizeOptional(sourceText),
            SourceUrl = NormalizeOptional(sourceUrl),
            ConversationUrl = NormalizeOptional(conversationUrl),
            MetadataJson = NormalizeOptional(metadataJson)
        };
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

[DebuggerDisplay("{Value}")]
public readonly record struct SourceRecordId
{
    public SourceRecordId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Source record id is required.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static SourceRecordId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

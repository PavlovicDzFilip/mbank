using System.Diagnostics;
using System.ComponentModel.DataAnnotations.Schema;
using MBank.Api.Services;
using NpgsqlTypes;
using Pgvector;

namespace MBank.Api.Domain;

public sealed class MemoryEntry
{
    public const string ActiveStatus = "active";
    public const string SupersededStatus = "superseded";

    private MemoryEntry()
    {
    }

    public MemoryId Id { get; private set; } = MemoryId.New();

    public MemoryType Type { get; private set; } = null!;

    public string Summary { get; private set; } = null!;

    public string? Subject { get; private set; }

    public string[] Tags { get; private set; } = [];

    public double Confidence { get; private set; } = 0.7;

    public string Status { get; private set; } = ActiveStatus;

    public UserId UserId { get; private set; }

    public ProjectId ProjectId { get; private set; }

    public string SearchText { get; private set; } = null!;

    public NpgsqlTsVector SearchVector { get; private set; } = null!;

    [Column(TypeName = "vector(1536)")]
    public Vector? Embedding { get; private set; }

    public SourceRecordId? SourceRecordId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public static async Task<MemoryEntry> CreateAsync(
        UserId userId,
        ProjectId projectId,
        MemoryType type,
        string summary,
        string? subject,
        IReadOnlyList<string>? tags,
        double confidence,
        string? sourceText,
        SourceRecordId? sourceRecordId,
        IEmbeddingService embeddings,
        CancellationToken cancellationToken)
    {
        type = type ?? throw new ArgumentNullException(nameof(type));
        embeddings = embeddings ?? throw new ArgumentNullException(nameof(embeddings));
        var normalizedSummary = RequiredText(summary, "Memory summary", 2000, nameof(summary));
        var normalizedSubject = OptionalText(subject, "Memory subject", 256, nameof(subject));
        var normalizedTags = NormalizeTags(tags);
        var searchText = BuildSearchText(type, normalizedSummary, normalizedSubject, normalizedTags, sourceText);
        var embedding = await embeddings.GenerateEmbeddingAsync(projectId, searchText, cancellationToken)
            ?? throw new InvalidOperationException("Memory embedding factory returned null.");

        return new MemoryEntry
        {
            UserId = userId,
            ProjectId = projectId,
            Type = type,
            Summary = normalizedSummary,
            Subject = normalizedSubject,
            Tags = normalizedTags,
            Confidence = Math.Clamp(confidence, 0, 1),
            SearchText = searchText,
            Embedding = embedding,
            SourceRecordId = sourceRecordId
        };
    }

    private static string BuildSearchText(
        MemoryType type,
        string summary,
        string? subject,
        IReadOnlyList<string>? tags,
        string? sourceText)
    {
        type = type ?? throw new ArgumentNullException(nameof(type));
        var normalizedSummary = RequiredText(summary, "Memory summary", 2000, nameof(summary));
        var normalizedSubject = OptionalText(subject, "Memory subject", 256, nameof(subject));
        var normalizedTags = NormalizeTags(tags);
        var sourceExcerpt = string.IsNullOrWhiteSpace(sourceText)
            ? ""
            : sourceText.Length <= 1200 ? sourceText : sourceText[..1200];

        var searchText = string.Join(
            "\n",
            $"Type: {type.Value}",
            $"Subject: {normalizedSubject}",
            $"Summary: {normalizedSummary}",
            $"Tags: {string.Join(", ", normalizedTags)}",
            $"Source excerpt: {sourceExcerpt}");

        return RequiredText(searchText, "Memory search text", 5000, nameof(summary));
    }

    public void Supersede()
    {
        if (Status == SupersededStatus)
        {
            return;
        }

        Status = SupersededStatus;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    public void ChangeType(MemoryType type)
    {
        if (Type == type)
        {
            return;
        }

        Type = type;
        UpdatedAt = DateTimeOffset.UtcNow;
    }

    private static string[] NormalizeTags(IReadOnlyList<string>? tags)
    {
        return tags?
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray() ?? [];
    }

    private static string RequiredText(string value, string displayName, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException($"{displayName} is required.", parameterName);
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{displayName} cannot exceed {maxLength} characters.", parameterName);
        }

        return trimmed;
    }

    private static string? OptionalText(string? value, string displayName, int maxLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (trimmed.Length > maxLength)
        {
            throw new ArgumentException($"{displayName} cannot exceed {maxLength} characters.", parameterName);
        }

        return trimmed;
    }
}

[DebuggerDisplay("{Value}")]
public readonly record struct MemoryId
{
    public MemoryId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Memory id is required.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static MemoryId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

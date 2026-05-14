using System.Diagnostics;
using System.ComponentModel.DataAnnotations.Schema;
using NpgsqlTypes;
using Pgvector;

namespace MBank.Api.Domain;

public sealed class MemoryEntry
{
    public const string ActiveStatus = "active";

    private MemoryEntry()
    {
    }

    public MemoryId Id { get; private set; } = MemoryId.New();

    public string Type { get; private set; } = null!;

    public string Summary { get; private set; } = null!;

    public string? Subject { get; private set; }

    public string? SubjectKind { get; private set; }

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

    public static MemoryEntry Create(
        UserId userId,
        ProjectId projectId,
        string type,
        string summary,
        string? subject,
        string? subjectKind,
        IReadOnlyList<string> tags,
        double confidence,
        string searchText,
        Vector embedding,
        SourceRecordId? sourceRecordId)
    {
        return new MemoryEntry
        {
            UserId = userId,
            ProjectId = projectId,
            Type = RequiredText(type, "Memory type", 64, nameof(type)),
            Summary = RequiredText(summary, "Memory summary", 2000, nameof(summary)),
            Subject = OptionalText(subject, "Memory subject", 256, nameof(subject)),
            SubjectKind = OptionalText(subjectKind, "Memory subject kind", 64, nameof(subjectKind)),
            Tags = NormalizeTags(tags),
            Confidence = Math.Clamp(confidence, 0, 1),
            SearchText = RequiredText(searchText, "Memory search text", 5000, nameof(searchText)),
            Embedding = embedding,
            SourceRecordId = sourceRecordId
        };
    }

    private static string[] NormalizeTags(IReadOnlyList<string> tags)
    {
        return tags
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(20)
            .ToArray();
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

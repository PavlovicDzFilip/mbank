using System.Diagnostics;

namespace MBank.Api.Domain;

public sealed class MemoryLink
{
    public const string ConflictRelation = "conflicts";

    private MemoryLink()
    {
    }

    public MemoryLinkId Id { get; private set; } = MemoryLinkId.New();

    public MemoryId FromMemoryId { get; private set; }

    public MemoryId ToMemoryId { get; private set; }

    public string Relation { get; private set; } = null!;

    public string? Description { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; } = DateTimeOffset.UtcNow;

    public static MemoryLink CreateConflict(MemoryId fromMemoryId, MemoryId toMemoryId, string? description)
    {
        return new MemoryLink
        {
            FromMemoryId = fromMemoryId,
            ToMemoryId = toMemoryId,
            Relation = ConflictRelation,
            Description = NormalizeOptional(description)
        };
    }

    public void ChangeDescription(string description)
    {
        Description = NormalizeOptional(description);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}

[DebuggerDisplay("{Value}")]
public readonly record struct MemoryLinkId
{
    public MemoryLinkId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Memory link id is required.", nameof(value));
        }

        Value = value;
    }

    public Guid Value { get; }

    public static MemoryLinkId New() => new(Guid.CreateVersion7());

    public override string ToString() => Value.ToString();
}

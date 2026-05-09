namespace MBank.Api.Domain;

public sealed class MemoryLink
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public Guid FromMemoryId { get; set; }

    public Guid ToMemoryId { get; set; }

    public string Relation { get; set; } = "related";

    public string? Description { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;
}

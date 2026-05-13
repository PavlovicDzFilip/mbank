using System.ComponentModel.DataAnnotations.Schema;
using NpgsqlTypes;
using Pgvector;

namespace MBank.Api.Domain;

public sealed class MemoryEntry
{
    public Guid Id { get; set; } = Guid.CreateVersion7();

    public string Type { get; set; } = "";

    public string Summary { get; set; } = "";

    public string? Subject { get; set; }

    public string? SubjectKind { get; set; }

    public string[] Tags { get; set; } = [];

    public double Confidence { get; set; } = 0.7;

    public string Status { get; set; } = "active";

    public Guid UserId { get; set; }

    public Guid ProjectId { get; set; }

    public string SearchText { get; set; } = "";

    public NpgsqlTsVector SearchVector { get; private set; } = null!;

    [Column(TypeName = "vector(1536)")]
    public Vector? Embedding { get; set; }

    public Guid? SourceRecordId { get; set; }

    public DateTimeOffset CreatedAt { get; set; } = DateTimeOffset.UtcNow;

    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}

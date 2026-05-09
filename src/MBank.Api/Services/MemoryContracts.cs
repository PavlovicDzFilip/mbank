namespace MBank.Api.Services;

public sealed record RememberRequest(
    IReadOnlyList<MemoryDraft> Memories,
    SourceDraft? Source = null);

public sealed record MemoryDraft(
    string Type,
    string Summary,
    string? Subject = null,
    string? SubjectKind = null,
    IReadOnlyList<string>? Tags = null,
    double? Confidence = null);

public sealed record SourceDraft(
    string? SourceKind = null,
    string? SourceText = null,
    string? SourceUrl = null,
    string? ConversationUrl = null,
    string? MetadataJson = null);

public sealed record RememberResponse(
    IReadOnlyList<StoredMemory> StoredMemories,
    IReadOnlyList<PossibleConflict> PossibleConflicts);

public sealed record StoredMemory(
    Guid Id,
    string Type,
    string Summary,
    string? Subject,
    string? SubjectKind,
    IReadOnlyList<string> Tags,
    double Confidence);

public sealed record PossibleConflict(
    Guid ExistingMemoryId,
    Guid? NewMemoryId,
    string ExistingSummary,
    string Reason,
    double Similarity);

public sealed record TaskContextRequest(
    TaskDescription Task,
    WorkArea? WorkArea = null,
    int Limit = 8);

public sealed record TaskDescription(
    string Title,
    string Description,
    string Kind);

public sealed record WorkArea(
    string? Subject = null,
    string? SubjectKind = null,
    IReadOnlyList<string>? Files = null,
    IReadOnlyList<string>? Modules = null);

public sealed record TaskContextResponse(
    string Status,
    string Summary,
    IReadOnlyList<RelevantMemory> RelevantMemories,
    IReadOnlyList<string> Warnings,
    IReadOnlyList<PossibleConflict> Conflicts,
    IReadOnlyList<string> ClarifyingQuestions);

public sealed record RelevantMemory(
    Guid Id,
    string Type,
    string Summary,
    string? Subject,
    string? SubjectKind,
    IReadOnlyList<string> Tags,
    double Confidence,
    double RelevanceScore,
    string WhyRelevant);

public sealed record MemoryDetail(
    Guid Id,
    string Type,
    string Summary,
    string? Subject,
    string? SubjectKind,
    IReadOnlyList<string> Tags,
    double Confidence,
    string Status,
    SourceDetail? Source,
    IReadOnlyList<Guid> LinkedMemoryIds,
    IReadOnlyList<MemoryLinkDetail> Links,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt);

public sealed record MemoryLinkDetail(
    Guid MemoryId,
    string Relation,
    string? Description,
    DateTimeOffset CreatedAt);

public sealed record SourceDetail(
    string? SourceKind,
    string? SourceText,
    string? SourceUrl,
    string? ConversationUrl,
    string? MetadataJson);

public sealed record ReportConflictRequest(
    IReadOnlyList<Guid> MemoryIds,
    string Description);

public sealed record ReportConflictResponse(
    bool Recorded,
    string Message);

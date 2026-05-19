using System.Text.Json.Serialization;
using MBank.Api.Domain;

namespace MBank.Api.Services;

public sealed record RememberRequest(
    IReadOnlyList<MemoryDraft> Memories,
    SourceDraft? Source = null);

public sealed record MemoryDraft(
    MemoryType Type,
    string Summary,
    string? Subject = null,
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
    MemoryType Type,
    string Summary,
    string? Subject,
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
    IReadOnlyList<string>? Files = null,
    IReadOnlyList<string>? Modules = null);

public sealed record TaskContextResponse(
    string Status,
    string Summary,
    IReadOnlyList<MemoryMatch> StrongMatches,
    IReadOnlyList<MemoryMatch> PossibleMatches,
    OnboardingStatusResponse Onboarding,
    bool CanProceed,
    IReadOnlyList<TaskContextAttentionItem> AttentionItems);

public sealed record StartTaskResponse(
    string Summary,
    IReadOnlyList<AgentRule> Rules,
    OnboardingStatusResponse Onboarding,
    string NextStep);

public sealed record AgentRule(
    string Text,
    string Source,
    Guid? MemoryId = null);

public sealed record TaskContextAttentionItem(
    string Kind,
    TaskContextAttentionAction Action,
    string Summary,
    string? Question = null,
    IReadOnlyList<Guid>? MemoryIds = null);

[JsonConverter(typeof(JsonStringEnumConverter<TaskContextAttentionAction>))]
public enum TaskContextAttentionAction
{
    [JsonStringEnumMemberName("notice")]
    Notice,

    [JsonStringEnumMemberName("ask_user")]
    AskUser
}

public sealed record MemoryMatch(
    Guid Id,
    MemoryType Type,
    string Summary,
    string? Subject,
    IReadOnlyList<string> Tags,
    double MemoryConfidence,
    double MatchScore,
    string MatchStrength);

public sealed record MemoryDetail(
    Guid Id,
    MemoryType Type,
    string Summary,
    string? Subject,
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

public sealed record ResolveConflictRequest(
    Guid KeptMemoryId,
    IReadOnlyList<Guid> SupersededMemoryIds,
    string Clarification);

public sealed record ResolveConflictResponse(
    bool Resolved,
    string Message,
    Guid? KeptMemoryId = null,
    IReadOnlyList<Guid>? SupersededMemoryIds = null);

public sealed record OnboardingStatusResponse(
    string Status,
    string Summary,
    string LatestVersion,
    string BootstrapScriptUrl,
    bool RequiresExplicitApproval,
    IReadOnlyList<AgentInstructionFileStatus> Files,
    IReadOnlyList<string> Warnings,
    string RecommendedAction);

public sealed record AgentInstructionFileStatus(
    string Target,
    string Path,
    string Status,
    string? InstalledVersion,
    string LatestVersion,
    string Message);

public sealed record AgentInstructionBlock(
    string Version,
    string Content);

public sealed record MemoryTypeCatalogResponse(
    int ModelVersion,
    IReadOnlyList<MemoryTypeDefinition> Types);

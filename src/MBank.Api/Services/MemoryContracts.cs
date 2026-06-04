using System.Text.Json.Serialization;
using MBank.Api.Domain;

namespace MBank.Api.Services;

public sealed record RememberRequest(
    IReadOnlyList<MemoryDraft> Memories,
    SourceDraft? Source = null);

public sealed record RememberToolRequest(
    IReadOnlyList<MemoryDraftInput> Memories,
    SourceDraft? Source = null)
{
    public RememberRequest ToRememberRequest()
    {
        return new RememberRequest(
            Memories.Select(x => x.ToMemoryDraft()).ToArray(),
            Source);
    }
}

public sealed record MemoryDraftInput(
    MemoryTypeValue Type,
    string Summary,
    string? Subject = null,
    IReadOnlyList<string>? Tags = null,
    double? Confidence = null,
    MemoryLifecycleValue Lifecycle = MemoryLifecycleValue.Accepted)
{
    public MemoryDraft ToMemoryDraft()
    {
        return new MemoryDraft(
            Type.ToMemoryType(),
            Summary,
            Subject,
            Tags,
            Confidence,
            Lifecycle);
    }
}

public sealed record MemoryDraft(
    MemoryType Type,
    string Summary,
    string? Subject = null,
    IReadOnlyList<string>? Tags = null,
    double? Confidence = null,
    MemoryLifecycleValue Lifecycle = MemoryLifecycleValue.Accepted);

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
    double Confidence,
    MemoryLifecycleValue Lifecycle,
    string Status);

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

[JsonConverter(typeof(JsonStringEnumConverter<MemoryTypeValue>))]
public enum MemoryTypeValue
{
    [JsonStringEnumMemberName("product-strategy")]
    ProductStrategy,

    [JsonStringEnumMemberName("product-decision")]
    ProductDecision,

    [JsonStringEnumMemberName("product-reasoning")]
    ProductReasoning,

    [JsonStringEnumMemberName("product-requirement")]
    ProductRequirement,

    [JsonStringEnumMemberName("product-constraint")]
    ProductConstraint,

    [JsonStringEnumMemberName("product-risk")]
    ProductRisk,

    [JsonStringEnumMemberName("product-open-question")]
    ProductOpenQuestion,

    [JsonStringEnumMemberName("technical-architecture")]
    TechnicalArchitecture,

    [JsonStringEnumMemberName("technical-decision")]
    TechnicalDecision,

    [JsonStringEnumMemberName("technical-reasoning")]
    TechnicalReasoning,

    [JsonStringEnumMemberName("technical-rule")]
    TechnicalRule,

    [JsonStringEnumMemberName("technical-convention")]
    TechnicalConvention,

    [JsonStringEnumMemberName("technical-preference")]
    TechnicalPreference,

    [JsonStringEnumMemberName("technical-constraint")]
    TechnicalConstraint,

    [JsonStringEnumMemberName("technical-risk")]
    TechnicalRisk,

    [JsonStringEnumMemberName("technical-open-question")]
    TechnicalOpenQuestion,

    [JsonStringEnumMemberName("implementation-result")]
    ImplementationResult,

    [JsonStringEnumMemberName("validation-result")]
    ValidationResult,

    [JsonStringEnumMemberName("operational-status")]
    OperationalStatus,

    [JsonStringEnumMemberName("operational-procedure")]
    OperationalProcedure,

    [JsonStringEnumMemberName("agent-instruction")]
    AgentInstruction,

    [JsonStringEnumMemberName("project-policy")]
    ProjectPolicy,

    [JsonStringEnumMemberName("user-preference")]
    UserPreference,

    [JsonStringEnumMemberName("collaboration-preference")]
    CollaborationPreference,

    [JsonStringEnumMemberName("note")]
    Note
}

[JsonConverter(typeof(JsonStringEnumConverter<MemoryLifecycleValue>))]
public enum MemoryLifecycleValue
{
    [JsonStringEnumMemberName("accepted")]
    Accepted,

    [JsonStringEnumMemberName("experimental")]
    Experimental
}

public static class MemoryTypeValueExtensions
{
    public static MemoryType ToMemoryType(this MemoryTypeValue value)
    {
        return value switch
        {
            MemoryTypeValue.ProductStrategy => MemoryType.ProductStrategy,
            MemoryTypeValue.ProductDecision => MemoryType.ProductDecision,
            MemoryTypeValue.ProductReasoning => MemoryType.ProductReasoning,
            MemoryTypeValue.ProductRequirement => MemoryType.ProductRequirement,
            MemoryTypeValue.ProductConstraint => MemoryType.ProductConstraint,
            MemoryTypeValue.ProductRisk => MemoryType.ProductRisk,
            MemoryTypeValue.ProductOpenQuestion => MemoryType.ProductOpenQuestion,
            MemoryTypeValue.TechnicalArchitecture => MemoryType.TechnicalArchitecture,
            MemoryTypeValue.TechnicalDecision => MemoryType.TechnicalDecision,
            MemoryTypeValue.TechnicalReasoning => MemoryType.TechnicalReasoning,
            MemoryTypeValue.TechnicalRule => MemoryType.TechnicalRule,
            MemoryTypeValue.TechnicalConvention => MemoryType.TechnicalConvention,
            MemoryTypeValue.TechnicalPreference => MemoryType.TechnicalPreference,
            MemoryTypeValue.TechnicalConstraint => MemoryType.TechnicalConstraint,
            MemoryTypeValue.TechnicalRisk => MemoryType.TechnicalRisk,
            MemoryTypeValue.TechnicalOpenQuestion => MemoryType.TechnicalOpenQuestion,
            MemoryTypeValue.ImplementationResult => MemoryType.ImplementationResult,
            MemoryTypeValue.ValidationResult => MemoryType.ValidationResult,
            MemoryTypeValue.OperationalStatus => MemoryType.OperationalStatus,
            MemoryTypeValue.OperationalProcedure => MemoryType.OperationalProcedure,
            MemoryTypeValue.AgentInstruction => MemoryType.AgentInstruction,
            MemoryTypeValue.ProjectPolicy => MemoryType.ProjectPolicy,
            MemoryTypeValue.UserPreference => MemoryType.UserPreference,
            MemoryTypeValue.CollaborationPreference => MemoryType.CollaborationPreference,
            MemoryTypeValue.Note => MemoryType.Note,
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown memory type.")
        };
    }
}

public sealed record MemoryMatch(
    Guid Id,
    MemoryType Type,
    string Summary,
    string? Subject,
    IReadOnlyList<string> Tags,
    double MemoryConfidence,
    double MatchScore,
    string MatchStrength,
    string Status,
    MemoryLifecycleValue Lifecycle);

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

public sealed record PromoteMemoryRequest(
    Guid MemoryId,
    IReadOnlyList<Guid>? SupersededMemoryIds = null,
    string? Clarification = null);

public sealed record PromoteMemoryResponse(
    bool Promoted,
    string Message,
    Guid? PromotedMemoryId = null,
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
    IReadOnlyList<MemoryTypeDefinition> Types,
    string CaptureOfferPolicy,
    ExperimentalMemoryGuidanceResponse ExperimentalMemoryGuidance);

public sealed record ExperimentalMemoryGuidanceResponse(
    IReadOnlyList<string> StartSignals,
    IReadOnlyList<string> EndSignals,
    IReadOnlyList<string> AgentRules);

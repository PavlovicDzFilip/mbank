using Ardalis.SmartEnum;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MBank.Api.Domain;

[JsonConverter(typeof(MemoryTypeJsonConverter))]
public sealed class MemoryType : SmartEnum<MemoryType, string>
{
    public const int ModelVersion = 1;

    public const string CaptureOfferPolicy =
        """
        After substantive exploration (ideas, direction, tradeoffs, or what is possible):
        1. Answer using get_task_context and the conversation.
        2. If durable candidates may matter in future sessions but were not explicitly committed, offer to store them in MBank. List one to three atomic candidates with suggested types from captureHint on describe_memory_types.
        3. Call remember only after the user agrees or explicitly asks to store something.
        4. Do not store rejected options, agent speculation the user did not adopt, or ordinary chat.
        Skip the offer when nothing is plausibly durable or the thread was purely meta.
        """;

    public static readonly MemoryType ProductStrategy = new(
        nameof(ProductStrategy),
        "product-strategy",
        "High-level product direction, scope, positioning, or strategic intent.",
        captureHint: "Offer when exploration surfaces tentative direction that is not yet a decision.");

    public static readonly MemoryType ProductDecision = new(
        nameof(ProductDecision),
        "product-decision",
        "A product choice that has been made and should guide future work.",
        captureHint: "Store when the user has chosen; offer if they just decided during exploration.");

    public static readonly MemoryType ProductReasoning = new(
        nameof(ProductReasoning),
        "product-reasoning",
        "Product rationale, tradeoff analysis, or why a product direction exists.",
        captureHint: "Offer for rationale or tradeoffs the user may want kept without a firm decision.");

    public static readonly MemoryType ProductRequirement = new(
        nameof(ProductRequirement),
        "product-requirement",
        "A required product behavior, acceptance criterion, or user-facing need.",
        captureHint: "Offer when exploration produced a firm requirement the user wants kept.");

    public static readonly MemoryType ProductConstraint = new(
        nameof(ProductConstraint),
        "product-constraint",
        "A product boundary or limitation that narrows acceptable solutions.",
        isWarning: true);

    public static readonly MemoryType ProductRisk = new(
        nameof(ProductRisk),
        "product-risk",
        "A product risk, hazard, or concern agents should keep visible.",
        isWarning: true);

    public static readonly MemoryType ProductOpenQuestion = new(
        nameof(ProductOpenQuestion),
        "product-open-question",
        "An unresolved product question that should not be silently decided.",
        captureHint: "Offer for unresolved product options or questions from exploration.");

    public static readonly MemoryType TechnicalArchitecture = new(
        nameof(TechnicalArchitecture),
        "technical-architecture",
        "Architecture, system shape, component boundaries, or structural design.",
        captureHint: "Offer when exploration establishes structural direction not yet decided.");

    public static readonly MemoryType TechnicalDecision = new(
        nameof(TechnicalDecision),
        "technical-decision",
        "A technical choice that has been made and should guide implementation.",
        captureHint: "Store when the user has chosen; offer if they just decided during exploration.");

    public static readonly MemoryType TechnicalReasoning = new(
        nameof(TechnicalReasoning),
        "technical-reasoning",
        "Technical rationale, tradeoff analysis, or why an implementation direction exists.",
        captureHint: "Offer for technical rationale the user may want kept without a firm decision.");

    public static readonly MemoryType TechnicalRule = new(
        nameof(TechnicalRule),
        "technical-rule",
        "A hard implementation rule, invariant, or coding convention agents must follow.",
        captureHint: "If the rule can be enforced by analyzers, EditorConfig, linter, or CI, suggest implementing tooling first and store a short memory with the diagnostic or rule id and rationale.");

    public static readonly MemoryType TechnicalConvention = new(
        nameof(TechnicalConvention),
        "technical-convention",
        "An established codebase or engineering practice that should guide implementation.",
        captureHint: "If the convention can be enforced by analyzers, EditorConfig, linter, or CI, suggest implementing tooling first and store a short memory with the diagnostic or rule id and rationale.");

    public static readonly MemoryType TechnicalPreference = new(
        nameof(TechnicalPreference),
        "technical-preference",
        "A softer technical style or implementation preference.",
        captureHint: "Offer when the user states a durable technical style preference during exploration. If automatable via tooling, suggest implementing enforcement first and link the diagnostic or rule id in the memory.");

    public static readonly MemoryType TechnicalConstraint = new(
        nameof(TechnicalConstraint),
        "technical-constraint",
        "A technical limitation, dependency, compatibility concern, or hard boundary.",
        isWarning: true);

    public static readonly MemoryType TechnicalRisk = new(
        nameof(TechnicalRisk),
        "technical-risk",
        "A technical risk, hazard, or failure mode agents should keep visible.",
        isWarning: true);

    public static readonly MemoryType TechnicalOpenQuestion = new(
        nameof(TechnicalOpenQuestion),
        "technical-open-question",
        "An unresolved technical question that should not be silently decided.",
        captureHint: "Offer for unresolved technical options or questions from exploration.");

    public static readonly MemoryType ImplementationResult = new(
        nameof(ImplementationResult),
        "implementation-result",
        "A completed implementation, refactor, migration, or code change result.");

    public static readonly MemoryType ValidationResult = new(
        nameof(ValidationResult),
        "validation-result",
        "A completed test, validation, verification, or quality check result.");

    public static readonly MemoryType OperationalStatus = new(
        nameof(OperationalStatus),
        "operational-status",
        "Current operational, deployment, environment, or runtime state.");

    public static readonly MemoryType OperationalProcedure = new(
        nameof(OperationalProcedure),
        "operational-procedure",
        "A repeatable operational, deployment, environment, or runtime procedure.");

    public static readonly MemoryType AgentInstruction = new(
        nameof(AgentInstruction),
        "agent-instruction",
        "Instruction for how agents should behave while working in the project.",
        isStartupGuidance: true);

    public static readonly MemoryType ProjectPolicy = new(
        nameof(ProjectPolicy),
        "project-policy",
        "Project-wide policy, workflow rule, or governance expectation.",
        isStartupGuidance: true);

    public static readonly MemoryType UserPreference = new(
        nameof(UserPreference),
        "user-preference",
        "Durable user working-style or collaboration preference.",
        isStartupGuidance: true,
        captureHint: "Offer when the user states a durable working-style preference during exploration.");

    public static readonly MemoryType CollaborationPreference = new(
        nameof(CollaborationPreference),
        "collaboration-preference",
        "Durable preference about how agents and the user should collaborate.",
        isStartupGuidance: true,
        captureHint: "Offer when the user states a durable collaboration preference during exploration.");

    public static readonly MemoryType Note = new(
        nameof(Note),
        "note",
        "Useful context that does not fit a more specific type.",
        captureHint: "Offer for vague ideas, half-formed thoughts, or useful context not yet a decision or open question.");

    private MemoryType(
        string name,
        string value,
        string description,
        bool isWarning = false,
        bool isStartupGuidance = false,
        string? captureHint = null)
        : base(name, value)
    {
        Description = description;
        IsWarning = isWarning;
        IsStartupGuidance = isStartupGuidance;
        CaptureHint = captureHint;
    }

    public string Description { get; }

    public bool IsWarning { get; }

    public bool IsStartupGuidance { get; }

    public string? CaptureHint { get; }

    public static IReadOnlyList<MemoryTypeDefinition> Definitions =>
        List.Select(x => new MemoryTypeDefinition(
            x.Value,
            x.Description,
            x.IsWarning,
            x.IsStartupGuidance,
            x.CaptureHint)).ToArray();
}

public sealed record MemoryTypeDefinition(
    string Value,
    string Description,
    bool IsWarning,
    bool IsStartupGuidance,
    string? CaptureHint = null);

public sealed class MemoryTypeJsonConverter : JsonConverter<MemoryType>
{
    public override MemoryType Read(
        ref Utf8JsonReader reader,
        Type typeToConvert,
        JsonSerializerOptions options)
    {
        var value = reader.GetString();
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new JsonException("Memory type is required.");
        }

        if (!MemoryType.TryFromValue(value, out var memoryType))
        {
            throw new JsonException($"Invalid memory type '{value}'.");
        }

        return memoryType;
    }

    public override void Write(
        Utf8JsonWriter writer,
        MemoryType value,
        JsonSerializerOptions options)
    {
        writer.WriteStringValue(value.Value);
    }
}

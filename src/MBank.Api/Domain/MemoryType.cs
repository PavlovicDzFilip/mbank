using Ardalis.SmartEnum;

namespace MBank.Api.Domain;

public sealed class MemoryType : SmartEnum<MemoryType, string>
{
    public const int ModelVersion = 1;

    public static readonly MemoryType ProductStrategy = new(
        nameof(ProductStrategy),
        "product-strategy",
        "High-level product direction, scope, positioning, or strategic intent.");

    public static readonly MemoryType ProductDecision = new(
        nameof(ProductDecision),
        "product-decision",
        "A product choice that has been made and should guide future work.");

    public static readonly MemoryType ProductReasoning = new(
        nameof(ProductReasoning),
        "product-reasoning",
        "Product rationale, tradeoff analysis, or why a product direction exists.");

    public static readonly MemoryType ProductRequirement = new(
        nameof(ProductRequirement),
        "product-requirement",
        "A required product behavior, acceptance criterion, or user-facing need.");

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
        "An unresolved product question that should not be silently decided.");

    public static readonly MemoryType TechnicalArchitecture = new(
        nameof(TechnicalArchitecture),
        "technical-architecture",
        "Architecture, system shape, component boundaries, or structural design.");

    public static readonly MemoryType TechnicalDecision = new(
        nameof(TechnicalDecision),
        "technical-decision",
        "A technical choice that has been made and should guide implementation.");

    public static readonly MemoryType TechnicalReasoning = new(
        nameof(TechnicalReasoning),
        "technical-reasoning",
        "Technical rationale, tradeoff analysis, or why an implementation direction exists.");

    public static readonly MemoryType TechnicalRule = new(
        nameof(TechnicalRule),
        "technical-rule",
        "A hard implementation rule, invariant, or coding convention agents must follow.");

    public static readonly MemoryType TechnicalConvention = new(
        nameof(TechnicalConvention),
        "technical-convention",
        "An established codebase or engineering practice that should guide implementation.");

    public static readonly MemoryType TechnicalPreference = new(
        nameof(TechnicalPreference),
        "technical-preference",
        "A softer technical style or implementation preference.");

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
        "An unresolved technical question that should not be silently decided.");

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
        isStartupGuidance: true);

    public static readonly MemoryType CollaborationPreference = new(
        nameof(CollaborationPreference),
        "collaboration-preference",
        "Durable preference about how agents and the user should collaborate.",
        isStartupGuidance: true);

    public static readonly MemoryType Note = new(
        nameof(Note),
        "note",
        "Useful context that does not fit a more specific type.");

    private MemoryType(
        string name,
        string value,
        string description,
        bool isWarning = false,
        bool isStartupGuidance = false)
        : base(name, value)
    {
        Description = description;
        IsWarning = isWarning;
        IsStartupGuidance = isStartupGuidance;
    }

    public string Description { get; }

    public bool IsWarning { get; }

    public bool IsStartupGuidance { get; }

    public static IReadOnlyList<MemoryTypeDefinition> Definitions =>
        List.Select(x => new MemoryTypeDefinition(x.Value, x.Description)).ToArray();
}

public sealed record MemoryTypeDefinition(string Value, string Description);

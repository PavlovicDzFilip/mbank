namespace MBank.Api.Services;

public static class MemoryAuthoringGuidance
{
    public static readonly IReadOnlyList<string> ExperimentalStartSignals =
    [
        "I want to experiment",
        "let's experiment",
        "let's explore",
        "explore options",
        "brainstorm",
        "ideate",
        "thinking out loud",
        "rough idea",
        "half-baked idea",
        "not final",
        "not durable yet",
        "not committed",
        "temporary idea",
        "draft",
        "sandbox",
        "spike",
        "prototype",
        "try something",
        "play with this",
        "test a hypothesis",
        "what if",
        "maybe",
        "could we",
        "I'm not sure",
        "compare approaches",
        "discovery mode",
        "research mode",
        "workshop this",
        "let's see where this goes"
    ];

    public static readonly IReadOnlyList<string> ExperimentalEndSignals =
    [
        "we decided",
        "let's go with",
        "this is the direction",
        "final decision",
        "that's settled",
        "make this durable",
        "store this",
        "remember this",
        "record this",
        "promote this",
        "accept this",
        "from now on",
        "new rule",
        "implement this",
        "ship this",
        "discard the experiment",
        "reject this",
        "keep this as an open question",
        "wrap this up",
        "close the loop",
        "end of experiment",
        "conclusion",
        "we converged",
        "not doing this"
    ];

    public static readonly IReadOnlyList<string> ExperimentalAgentRules =
    [
        "When a user uses an experiment-start signal, treat ideas as provisional. Do not store them as accepted durable memory unless the user explicitly asks for durable storage.",
        "If the user asks to store a provisional idea, call remember with lifecycle `experimental`; experimental memories are hidden from normal task recall and must not supersede accepted memories.",
        "When an experiment appears to converge or the user uses an experiment-end signal, summarize the likely durable outcome and ask whether to promote, reject, or keep it experimental.",
        "Call promote_memory only after the user confirms an experimental memory should become accepted. Promotion is the point where an experimental memory may supersede older accepted memories.",
        "Rejected ideas, ordinary brainstorming, and agent speculation should not be promoted."
    ];

    public static readonly ExperimentalMemoryGuidanceResponse ExperimentalMemoryGuidance = new(
        ExperimentalStartSignals,
        ExperimentalEndSignals,
        ExperimentalAgentRules);

    public const string ExperimentalLifecycleParagraph = """
        Experimental memory lifecycle: use lifecycle `experimental` for user-approved provisional ideas, hypotheses, spikes, drafts, brainstorm outcomes, and discovery notes that should be kept for review but should not guide normal work yet. Experimental memories are hidden from normal task recall and cannot supersede accepted memories until the user explicitly promotes them with promote_memory. Treat phrases like "let's experiment", "explore options", "brainstorm", "rough idea", "not final", "draft", "spike", "prototype", "what if", "maybe", and "thinking out loud" as experiment-start signals. Treat phrases like "let's go with", "this is the direction", "final decision", "make this durable", "remember this", "promote this", "implement this", "we converged", "discard the experiment", and "keep this as an open question" as experiment-end signals that should trigger a promotion/rejection/keep-experimental offer.
        """;

    public const string MechanicalEnforcementParagraph = """
        When storing a technical-rule, technical-convention, or technical-preference, consider whether the repo can enforce it mechanically (for example a Roslyn analyzer, EditorConfig rule, linter, or CI check in this .NET solution). If yes, tell the user and recommend implementing that tooling first, then store a short memory with rationale and the diagnostic or rule identifier. Do not rely on memory alone for rules that can be automated. Keep nuanced exceptions and rationale in memory even when tooling exists.
        """;

    public const string RememberToolDescription = """
        Store durable product or implementation guidance that is likely to matter in future sessions. Each memory type must be one of the valid MemoryType values returned by describe_memory_types. Do not call without user approval unless the user explicitly asks to store something. After exploration, offer capture first; on approval, store atomic memories using captureHint guidance from describe_memory_types. When the user asks why one approach was chosen instead of another, treat it as a strong signal that an assumption, preference, or rule may have been missed; offer to store durable guidance if the exchange reveals it, but do not store the agent's guess as fact before the user confirms the underlying guidance. Do not store ordinary conversation, temporary reasoning, agent brainstorm output, or information only relevant to the current response. Send short atomic memories; include source text when available.
        """ + " " + ExperimentalLifecycleParagraph + " " + MechanicalEnforcementParagraph;

    public const string CanonicalMechanicalEnforcementSection = """
        ## Mechanical enforcement for coding rules

        When calling `remember` for a `technical-rule`, `technical-convention`, or `technical-preference`, consider whether the repo can enforce it with tooling (Roslyn analyzer, EditorConfig, linter, or CI in this .NET solution). If yes, tell the user and recommend implementing that tooling first, then store a short memory with rationale and the diagnostic or rule identifier. Do not rely on memory alone for automatable rules. Keep nuanced exceptions in memory even when tooling exists.
        """;
}

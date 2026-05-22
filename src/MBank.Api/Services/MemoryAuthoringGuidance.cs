namespace MBank.Api.Services;

public static class MemoryAuthoringGuidance
{
    public const string MechanicalEnforcementParagraph = """
        When storing a technical-rule, technical-convention, or technical-preference, consider whether the repo can enforce it mechanically (for example a Roslyn analyzer, EditorConfig rule, linter, or CI check in this .NET solution). If yes, tell the user and recommend implementing that tooling first, then store a short memory with rationale and the diagnostic or rule identifier. Do not rely on memory alone for rules that can be automated. Keep nuanced exceptions and rationale in memory even when tooling exists.
        """;

    public const string RememberToolDescription = """
        Store durable product or implementation guidance that is likely to matter in future sessions. Each memory type must be one of the valid MemoryType values returned by describe_memory_types. Do not call without user approval unless the user explicitly asks to store something. After exploration, offer capture first; on approval, store atomic memories using captureHint guidance from describe_memory_types. Do not store ordinary conversation, temporary reasoning, agent brainstorm output, or information only relevant to the current response. Send short atomic memories; include source text when available.
        """ + " " + MechanicalEnforcementParagraph;

    public const string CanonicalMechanicalEnforcementSection = """
        ## Mechanical enforcement for coding rules

        When calling `remember` for a `technical-rule`, `technical-convention`, or `technical-preference`, consider whether the repo can enforce it with tooling (Roslyn analyzer, EditorConfig, linter, or CI in this .NET solution). If yes, tell the user and recommend implementing that tooling first, then store a short memory with rationale and the diagnostic or rule identifier. Do not rely on memory alone for automatable rules. Keep nuanced exceptions in memory even when tooling exists.
        """;
}

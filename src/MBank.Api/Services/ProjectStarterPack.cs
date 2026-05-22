using MBank.Api.Domain;

namespace MBank.Api.Services;

/// <summary>
/// Default agent guidance seeded into every new project.
/// Curated from production MBank memories (235 active) plus AGENTS.md,
/// generalized so they apply outside the MBank repository itself.
/// </summary>
public static class ProjectStarterPack
{
    public const string SourceKind = "starter-pack";

    public static readonly IReadOnlyList<MemoryDraft> Memories =
    [
        new(
            MemoryType.AgentInstruction,
            "At the start of product, planning, implementation, debugging, or review work, call `start_task` and follow its rules, then call `get_task_context` for the current focus. Re-call `get_task_context` when conversation focus changes before substantive project-specific answers, including exploration.",
            "MBank task startup",
            ["mbank", "mcp", "startup"],
            0.98),
        new(
            MemoryType.AgentInstruction,
            "When repo agent instructions reference the configured `mbank` MCP server, invoke MBank tools at task start—not only inject instruction file text into context.",
            "MBank MCP invocation",
            ["mbank", "mcp", "cursor", "agents"],
            0.95),
        new(
            MemoryType.AgentInstruction,
            "Use MBank memory as the source of continuity for product direction, decisions, open questions, constraints, and next steps. Do not recreate local continuity files such as CURRENT_WORK.md or PRODUCT_BOARD.md unless the user explicitly asks.",
            "MBank continuity",
            ["mbank", "continuity"],
            0.98),
        new(
            MemoryType.AgentInstruction,
            "Use `remember` only for durable product or implementation guidance the user provided or explicitly agreed to store. Do not store ordinary conversation, temporary reasoning, agent brainstorm output, or details only relevant to the current response.",
            "Memory capture",
            ["mbank", "mcp", "remember"],
            0.98),
        new(
            MemoryType.CollaborationPreference,
            "After substantive exploration (ideas, direction, tradeoffs), offer to store durable candidates in MBank with suggested types from describe_memory_types captureHint. Call remember only after the user agrees.",
            "Offer MBank capture after exploration",
            ["mbank", "mcp", "remember", "exploration"],
            0.98),
        new(
            MemoryType.AgentInstruction,
            "Never store local repository paths in MBank memory. Use repository-relative paths only when file context or source evidence is needed.",
            "Repository paths",
            ["mbank", "paths", "sources"],
            0.98),
        new(
            MemoryType.AgentInstruction,
            "Never store secret values, credentials, or tokens in tracked files, planning notes, agent instruction files, or MBank memory.",
            "Secrets handling",
            ["security", "secrets"],
            1),
        new(
            MemoryType.AgentInstruction,
            "When memories appear inconsistent, surface the conflict to the user. Do not decide which memory is true automatically.",
            "Conflict handling",
            ["mbank", "conflicts"],
            0.98),
        new(
            MemoryType.CollaborationPreference,
            "Be a sparring partner: push back on vague goals, weak assumptions, unnecessary complexity, risky tradeoffs, and product decisions that do not clearly serve the user outcome. Distinguish understanding an idea from endorsing it.",
            "Sparring partner",
            ["collaboration", "product"],
            0.98),
        new(
            MemoryType.CollaborationPreference,
            "Do not make code or implementation file changes without explicit user permission. Discuss the product goal first, propose a technical approach, then implement after a clear go-ahead.",
            "Collaboration workflow",
            ["workflow", "approval"],
            0.98),
        new(
            MemoryType.CollaborationPreference,
            "When an issue appears even though there is evidence the requested approach should work, investigate why the expected path is failing before adding workaround complexity. Present the user with facts and clear options before proceeding.",
            "Investigate before workarounds",
            ["debugging", "collaboration"],
            0.98),
        new(
            MemoryType.CollaborationPreference,
            "Prefer delivering large features as smaller, reviewable slices rather than one monolithic change.",
            "Incremental delivery",
            ["delivery", "review"],
            0.95),
        new(
            MemoryType.UserPreference,
            "Ask important clarifying questions upfront so later work can proceed with more autonomy and fewer interruptions.",
            "Upfront clarification",
            ["collaboration", "planning"],
            0.98),
        new(
            MemoryType.UserPreference,
            "For large features, agree on a plan and resolve important open questions before implementation begins.",
            "Plan before large work",
            ["planning", "scope"],
            0.98),
        new(
            MemoryType.UserPreference,
            "Once product direction and key constraints are clear, proceed autonomously through implementation and verification within the agreed scope.",
            "Autonomy after alignment",
            ["implementation", "scope"],
            0.98),
        new(
            MemoryType.UserPreference,
            "Prefer small, focused commits rather than large bundled commits.",
            "Commit style",
            ["git", "workflow"],
            0.98),
        new(
            MemoryType.UserPreference,
            "Prefer the simplest correct solution for the current task. Do not build for hypothetical future needs unless the user explicitly asks.",
            "Simplicity",
            ["scope", "design"],
            0.98),
        new(
            MemoryType.UserPreference,
            "Prefer the smallest sufficient change. Avoid extra abstractions or enhancements that are not necessary; ask before adding them.",
            "Minimal change",
            ["scope", "design"],
            0.98),
        new(
            MemoryType.UserPreference,
            "Prefer fail-fast engineering in this order: compile-time errors when possible, then startup-time configuration validation, then test-time failures, with runtime bugs last.",
            "Fail-fast engineering",
            ["engineering", "quality"],
            0.98),
        new(
            MemoryType.UserPreference,
            "Before adding any unrequested flag, option, fallback, bypass, alternate mode, or convenience behavior, stop and ask first. Treat such additions as scope expansion requiring explicit user permission.",
            "Scope expansion",
            ["scope", "approval"],
            0.98),
        new(
            MemoryType.ProjectPolicy,
            "Start by discussing the product goal and desired user outcome before proposing a technical solution.",
            "Product-first discussion",
            ["product", "workflow"],
            0.98),
        new(
            MemoryType.ProjectPolicy,
            "Implement only after the user gives a clear go-ahead when scope, approach, or tradeoffs are still uncertain.",
            "Explicit go-ahead",
            ["workflow", "approval"],
            0.98),
    ];
}

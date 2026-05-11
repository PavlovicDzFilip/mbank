using System.ComponentModel;
using MBank.Api.Services;
using ModelContextProtocol.Server;

namespace MBank.Api.Mcp;

[McpServerToolType]
public static class MemoryTools
{
    [McpServerTool(ReadOnly = false, Destructive = false)]
    [Description("Store durable product or implementation guidance that is likely to matter in future sessions. Do not store ordinary conversation, temporary reasoning, or information only relevant to the current response. Send short atomic memories; include source text when available.")]
    public static Task<RememberResponse> Remember(
        IMemoryService memoryService,
        [Description("One or more short atomic memories plus optional source evidence.")] RememberRequest request,
        CancellationToken cancellationToken)
    {
        return memoryService.RememberAsync(request, cancellationToken);
    }

    [McpServerTool(ReadOnly = true)]
    [Description("Get relevant memories before planning or implementing a task. Call this at the start of product, planning, implementation, debugging, or review work. Strong matches may guide work; possible matches are hints to confirm before relying on them. Always inspect canProceed and attentionItems. If canProceed is false or any attention item has action=ask_user, stop and ask the user the provided question before planning or implementing. The response may also warn when MBank agent instruction files are missing or outdated; ask the user before updating files or running bootstrap scripts.")]
    public static Task<TaskContextResponse> GetTaskContext(
        IMemoryService memoryService,
        [Description("Structured description of the task and optional work area.")] TaskContextRequest request,
        CancellationToken cancellationToken)
    {
        return memoryService.GetTaskContextAsync(request, cancellationToken);
    }

    [McpServerTool(ReadOnly = true)]
    [Description("Fetch one memory with source evidence and linked memories by id.")]
    public static Task<MemoryDetail?> Get(
        IMemoryService memoryService,
        [Description("Memory id to fetch.")] Guid id,
        CancellationToken cancellationToken)
    {
        return memoryService.GetAsync(id, cancellationToken);
    }

    [McpServerTool(ReadOnly = false, Destructive = false)]
    [Description("Report that two or more memories appear to conflict. This does not resolve truth; it records that the conflict should be surfaced for clarification.")]
    public static Task<ReportConflictResponse> ReportConflict(
        IMemoryService memoryService,
        [Description("Conflict report containing related memory ids and a short description.")] ReportConflictRequest request,
        CancellationToken cancellationToken)
    {
        return memoryService.ReportConflictAsync(request, cancellationToken);
    }

    [McpServerTool(ReadOnly = true)]
    [Description("Check whether supported repo agent instruction files contain the current versioned MBank onboarding block. This tool never edits files. If instructions are missing or outdated, notify the user and require explicit approval before updating files or running the bootstrap script.")]
    public static OnboardingStatusResponse CheckOnboarding(IAgentInstructionService agentInstructions)
    {
        return agentInstructions.CheckOnboarding();
    }
}

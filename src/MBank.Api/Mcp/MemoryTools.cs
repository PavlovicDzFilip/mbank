using System.ComponentModel;
using MBank.Api.Domain;
using MBank.Api.Services;
using ModelContextProtocol.Server;

namespace MBank.Api.Mcp;

[McpServerToolType]
public static class MemoryTools
{
    [McpServerTool(ReadOnly = true)]
    [Description("Begin a product, planning, implementation, debugging, or review task by retrieving current MBank agent rules and working-style guidance. Follow the returned rules for the current task, then call get_task_context for task-specific product and technical context. This tool never edits files; if instruction files are missing, outdated, or not checkable from the server, ask the user before updating them or running bootstrap scripts.")]
    public static Task<StartTaskResponse> StartTask(
        MemoryService memoryService,
        CancellationToken cancellationToken)
    {
        return memoryService.StartTaskAsync(cancellationToken);
    }

    [McpServerTool(ReadOnly = false, Destructive = false)]
    [Description(MemoryAuthoringGuidance.RememberToolDescription)]
    public static Task<RememberResponse> Remember(
        MemoryService memoryService,
        [Description("One or more short atomic memories plus optional source evidence.")] RememberToolRequest request,
        CancellationToken cancellationToken)
    {
        return memoryService.RememberAsync(request.ToRememberRequest(), cancellationToken);
    }

    [McpServerTool(ReadOnly = true)]
    [Description("Get relevant memories for the current task focus. Call at task start, whenever focus shifts (new topic, product area, technical subject, or phase), when uncertain how to proceed, before choosing between materially different technical approaches, and before specific implementation decisions that may depend on project history, prior decisions, constraints, or user preferences. Skip on trivial same-focus follow-ups. Strong matches may guide work; possible matches are hints to confirm before relying on them. Always inspect canProceed and attentionItems. A capture_offer attention item means offer to store durable exploration outcomes after answering, then call remember only if the user agrees. If canProceed is false or any attention item has action=ask_user, stop and ask the user the provided question before planning or implementing. The response may also warn when MBank agent instruction files are missing, outdated, or not checkable from the server; ask the user before updating files or running bootstrap scripts.")]
    public static Task<TaskContextResponse> GetTaskContext(
        MemoryService memoryService,
        [Description("Structured description of the task and optional work area.")] TaskContextRequest request,
        CancellationToken cancellationToken)
    {
        return memoryService.GetTaskContextAsync(request, cancellationToken);
    }

    [McpServerTool(ReadOnly = true)]
    [Description("Fetch one memory with source evidence and linked memories by id.")]
    public static Task<MemoryDetail?> Get(
        MemoryService memoryService,
        [Description("Memory id to fetch.")] Guid id,
        CancellationToken cancellationToken)
    {
        return memoryService.GetAsync(id, cancellationToken);
    }

    [McpServerTool(ReadOnly = false, Destructive = false)]
    [Description("Report that two or more memories appear to conflict. This does not resolve truth; it records that the conflict should be surfaced for clarification.")]
    public static Task<ReportConflictResponse> ReportConflict(
        MemoryService memoryService,
        [Description("Conflict report containing related memory ids and a short description.")] ReportConflictRequest request,
        CancellationToken cancellationToken)
    {
        return memoryService.ReportConflictAsync(request, cancellationToken);
    }

    [McpServerTool(ReadOnly = false, Destructive = false)]
    [Description("Resolve a clarified memory conflict by keeping the true memory, marking the others superseded, and linking the kept memory to the superseded memories. Superseded memories are hidden from normal retrieval but remain available by direct id for audit.")]
    public static Task<ResolveConflictResponse> ResolveConflict(
        MemoryService memoryService,
        [Description("Conflict resolution with the memory to keep, memories to supersede, and the user's clarification.")] ResolveConflictRequest request,
        CancellationToken cancellationToken)
    {
        return memoryService.ResolveConflictAsync(request, cancellationToken);
    }

    [McpServerTool(ReadOnly = true)]
    [Description("Describe the canonical MemoryType values accepted by remember, including descriptions, behavioral flags, and captureHint guidance for offering to store ideas or exploration outcomes. Also returns captureOfferPolicy for when to offer remember versus calling remember silently. Use this when choosing a memory type or preparing a capture offer.")]
    public static MemoryTypeCatalogResponse DescribeMemoryTypes()
    {
        return new MemoryTypeCatalogResponse(
            MemoryType.ModelVersion,
            MemoryType.Definitions,
            MemoryType.CaptureOfferPolicy);
    }

    [McpServerTool(ReadOnly = true)]
    [Description("Check whether supported repo agent instruction files contain the current versioned MBank onboarding block. This tool never edits files. If instructions are missing, outdated, or not checkable from the server, notify the user and require explicit approval before updating files or running the bootstrap script.")]
    public static OnboardingStatusResponse CheckOnboarding(AgentInstructionService agentInstructions)
    {
        return agentInstructions.CheckOnboarding();
    }
}

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
    [Description("Get relevant memories before planning or implementing a task. Send structured task data: title, description, kind, and optional work area such as subject, files, or modules. This tool performs retrieval; do not use keyword-search behavior manually.")]
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
}


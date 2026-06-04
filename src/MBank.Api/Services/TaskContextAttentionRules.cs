namespace MBank.Api.Services;

public static class TaskContextAttentionRules
{
    public static bool ShouldIncludeExperimentalMemories(TaskContextRequest request)
    {
        var text = string.Join(
            " ",
            request.Task.Kind,
            request.Task.Title,
            request.Task.Description,
            request.WorkArea?.Subject,
            string.Join(" ", request.WorkArea?.Modules ?? []),
            string.Join(" ", request.WorkArea?.Files ?? []));

        return MemoryAuthoringGuidance.ExperimentalStartSignals.Any(signal =>
                text.Contains(signal, StringComparison.OrdinalIgnoreCase))
            || text.Contains("experiment", StringComparison.OrdinalIgnoreCase)
            || text.Contains("explor", StringComparison.OrdinalIgnoreCase)
            || text.Contains("brainstorm", StringComparison.OrdinalIgnoreCase)
            || text.Contains("prototype", StringComparison.OrdinalIgnoreCase)
            || text.Contains("spike", StringComparison.OrdinalIgnoreCase)
            || text.Contains("draft", StringComparison.OrdinalIgnoreCase)
            || text.Contains("hypothesis", StringComparison.OrdinalIgnoreCase)
            || text.Contains("provisional", StringComparison.OrdinalIgnoreCase)
            || text.Contains("discovery", StringComparison.OrdinalIgnoreCase);
    }

    public static bool ShouldOfferCapture(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        return kind.Contains("plan", StringComparison.OrdinalIgnoreCase)
            || kind.Contains("explor", StringComparison.OrdinalIgnoreCase)
            || kind.Contains("idea", StringComparison.OrdinalIgnoreCase)
            || kind.Contains("direction", StringComparison.OrdinalIgnoreCase)
            || kind.Contains("brainstorm", StringComparison.OrdinalIgnoreCase);
    }

    public static TaskContextAttentionItem CreateCaptureOfferItem()
    {
        return new TaskContextAttentionItem(
            "capture_offer",
            TaskContextAttentionAction.Notice,
            "After answering, offer to store durable ideas, open questions, tentative direction, or notes the user may want later. Call `remember` only after the user agrees.",
            "Would you like to store any of this in MBank for future sessions?");
    }
}

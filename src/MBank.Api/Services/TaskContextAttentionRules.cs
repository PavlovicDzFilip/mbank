namespace MBank.Api.Services;

public static class TaskContextAttentionRules
{
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

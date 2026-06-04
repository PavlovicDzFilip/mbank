using MBank.Api.Services;

namespace MBank.Tests;

public sealed class TaskContextAttentionRulesTests
{
    [Theory]
    [InlineData("planning", true)]
    [InlineData("product-planning", true)]
    [InlineData("exploration", true)]
    [InlineData("ideas", true)]
    [InlineData("direction", true)]
    [InlineData("implementation", false)]
    [InlineData("debugging", false)]
    [InlineData("", false)]
    public void ShouldOfferCapture_matches_exploration_task_kinds(string kind, bool expected)
    {
        Assert.Equal(expected, TaskContextAttentionRules.ShouldOfferCapture(kind));
    }

    [Fact]
    public void CreateCaptureOfferItem_uses_capture_offer_kind_and_notice_action()
    {
        var item = TaskContextAttentionRules.CreateCaptureOfferItem();

        Assert.Equal("capture_offer", item.Kind);
        Assert.Equal(TaskContextAttentionAction.Notice, item.Action);
        Assert.Contains("offer", item.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("remember", item.Summary, StringComparison.OrdinalIgnoreCase);
        Assert.False(string.IsNullOrWhiteSpace(item.Question));
    }

    [Theory]
    [InlineData("Let's experiment with a rough idea for memory sessions.")]
    [InlineData("This is thinking out loud, not final, but compare approaches.")]
    [InlineData("Run a spike and prototype the discovery mode.")]
    [InlineData("What if we keep this as a draft hypothesis?")]
    public void ShouldIncludeExperimentalMemories_matches_experiment_start_phrases(string description)
    {
        var request = new TaskContextRequest(
            new TaskDescription(
                "Explore provisional memory",
                description,
                "planning"));

        Assert.True(TaskContextAttentionRules.ShouldIncludeExperimentalMemories(request));
    }

    [Fact]
    public void ShouldIncludeExperimentalMemories_ignores_work_area_metadata()
    {
        var request = new TaskContextRequest(
            new TaskDescription(
                "Implement lifecycle guidance",
                "Use memory lifecycle guidance for normal implementation work.",
                "implementation"),
            new WorkArea(
                Subject: "Experimental lifecycle",
                Modules: ["discovery mode", "prototype"]));

        Assert.False(TaskContextAttentionRules.ShouldIncludeExperimentalMemories(request));
    }
}

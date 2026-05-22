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
}

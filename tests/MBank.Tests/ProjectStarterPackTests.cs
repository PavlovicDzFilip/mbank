using MBank.Api.Domain;
using MBank.Api.Services;

namespace MBank.Tests;

public sealed class ProjectStarterPackTests
{
    [Fact]
    public void Starter_pack_contains_only_startup_guidance_types()
    {
        Assert.All(ProjectStarterPack.Memories, memory =>
            Assert.True(memory.Type.IsStartupGuidance, memory.Type.Value));
    }

    [Fact]
    public void Starter_pack_includes_core_agent_and_collaboration_guidance()
    {
        var summaries = ProjectStarterPack.Memories.Select(x => x.Summary).ToArray();

        Assert.Contains(summaries, x => x.Contains("start_task", StringComparison.Ordinal));
        Assert.Contains(summaries, x => x.Contains("uncertain how to proceed", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(summaries, x => x.Contains("why one approach was chosen instead of another", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(summaries, x => x.Contains("sparring partner", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(summaries, x => x.Contains("small, focused commits", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(summaries, x => x.Contains("fail-fast", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(summaries, x => x.Contains("repository-relative paths", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Starter_pack_has_no_mbank_repository_specific_paths()
    {
        Assert.All(ProjectStarterPack.Memories, memory =>
        {
            Assert.DoesNotContain(@"D:\", memory.Summary, StringComparison.Ordinal);
            Assert.DoesNotContain("trees\\", memory.Summary, StringComparison.Ordinal);
        });
    }
}

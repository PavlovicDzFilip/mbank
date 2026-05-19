using MBank.Api.Domain;

namespace MBank.Tests;

public sealed class MemoryTypeTests
{
    [Fact]
    public void MemoryType_defines_versioned_canonical_values_for_storage_and_contracts()
    {
        Assert.Equal(1, MemoryType.ModelVersion);
        Assert.Contains(MemoryType.List, x => x == MemoryType.ProductDecision && x.Value == "product-decision");
        Assert.Contains(MemoryType.List, x => x == MemoryType.TechnicalRule && x.Value == "technical-rule");
        Assert.Contains(MemoryType.List, x => x == MemoryType.AgentInstruction && x.Value == "agent-instruction");
        Assert.All(MemoryType.Definitions, definition => Assert.False(string.IsNullOrWhiteSpace(definition.Description)));
    }
}

using System.Text.Json;
using MBank.Api.Domain;
using MBank.Api.Services;

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
        Assert.Contains(MemoryType.Definitions, x => x.Value == "product-risk" && x.IsWarning);
        Assert.Contains(MemoryType.Definitions, x => x.Value == "agent-instruction" && x.IsStartupGuidance);
    }

    [Fact]
    public void MemoryTypeValue_matches_canonical_memory_type_values()
    {
        var options = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        var exposedValues = Enum.GetValues<MemoryTypeValue>()
            .Select(value => JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(value, options), options)!)
            .ToArray();
        var canonicalValues = MemoryType.Definitions
            .Select(x => x.Value)
            .ToArray();

        Assert.Equal(canonicalValues.Order(), exposedValues.Order());

        foreach (var value in Enum.GetValues<MemoryTypeValue>())
        {
            var exposedValue = JsonSerializer.Deserialize<string>(JsonSerializer.Serialize(value, options), options);
            Assert.Equal(exposedValue, value.ToMemoryType().Value);
        }
    }
}

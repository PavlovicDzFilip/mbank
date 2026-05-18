extern alias MigrationService;
using MBank.Api.Domain;
using MigrationService::MBank.MigrationService.CodeMigrations;

namespace MBank.Tests;

public sealed class StandardizeMemoryTypesTests
{
    [Fact]
    public void StandardizedTypes_contains_reviewed_production_snapshot()
    {
        Assert.Equal(212, StandardizeMemoryTypes.StandardizedTypes.Count);
        Assert.Contains(StandardizeMemoryTypes.StandardizedTypes.Values, x => x == MemoryType.TechnicalConvention);
        Assert.Contains(StandardizeMemoryTypes.StandardizedTypes.Values, x => x == MemoryType.OperationalProcedure);
    }

    [Fact]
    public void StandardizedTypes_uses_only_canonical_memory_types()
    {
        Assert.All(
            StandardizeMemoryTypes.StandardizedTypes.Values,
            type => Assert.Contains(MemoryType.List, canonicalType => canonicalType == type));
    }
}

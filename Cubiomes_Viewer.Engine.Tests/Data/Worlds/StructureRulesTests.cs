using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Tests.Data.Worlds;

public sealed class StructureRulesTests
{
    [Fact]
    public void Salts_InAnyOrder_GiveEqualRules()
    {
        var first = new StructureRules { Salts = [new StructureSalt(StructureType.Village, 1), new StructureSalt(StructureType.Igloo, 2)] };
        var second = new StructureRules { Salts = [new StructureSalt(StructureType.Igloo, 2), new StructureSalt(StructureType.Village, 1)] };

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
        Assert.Equal([StructureType.Igloo, StructureType.Village], first.Salts.Select(salt => salt.Type));
    }

    [Fact]
    public void Salts_TwiceForOneType_KeepTheLast()
    {
        var rules = new StructureRules { Salts = [new StructureSalt(StructureType.Village, 1), new StructureSalt(StructureType.Village, 7)] };

        Assert.True(rules.TryGetSalt(StructureType.Village, out var salt));
        Assert.Equal(7, salt);
        Assert.Single(rules.Salts);
    }

    [Fact]
    public void WorldKey_WithTheGameRulesSpelledOut_EqualsOneWithout()
    {
        var plain = new WorldKey(1, McVersion.V1_21_3, Dimension.Overworld, GeneratorFlags.None);
        var spelledOut = plain with { Rules = new StructureRules() };

        Assert.Equal(plain, spelledOut);
        Assert.Equal(plain.GetHashCode(), spelledOut.GetHashCode());
        Assert.Same(StructureRules.Default, default(WorldKey).Rules);
    }

    [Fact]
    public void WorldKey_WithOtherRules_DiffersFromTheGameOnes()
    {
        var plain = new WorldKey(1, McVersion.V1_21_3, Dimension.Overworld, GeneratorFlags.None);

        Assert.NotEqual(plain, plain with { Rules = new StructureRules { EstimateTerrain = false } });
        Assert.Equal(plain with { Rules = new StructureRules { EstimateTerrain = false } }, plain with { Rules = new StructureRules { EstimateTerrain = false } });
    }
}

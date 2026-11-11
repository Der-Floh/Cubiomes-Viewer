using System.Text.Json;

using Cubiomes_Viewer.Engine.Data.Biomes;

namespace Cubiomes_Viewer.Engine.Tests.Data;

public sealed class BiomeSetTests
{
    [Fact]
    public void Empty_HasNoBiomes()
    {
        Assert.True(BiomeSet.Empty.IsEmpty);
        Assert.Equal(0, BiomeSet.Empty.Count);
        Assert.Empty(BiomeSet.Empty.ToArray());
    }

    [Fact]
    public void Of_BiomesInEveryWord_ContainsExactlyThem()
    {
        var set = BiomeSet.Of(0, 63, 64, 129, 255);

        Assert.Equal([0, 63, 64, 129, 255], set.ToArray());
        Assert.Equal(5, set.Count);
        Assert.True(set.Contains(129));
        Assert.False(set.Contains(1));
        Assert.False(set.Contains(-1));
        Assert.False(set.Contains(256));
    }

    [Fact]
    public void With_SameBiomesInAnotherOrder_IsEqual()
    {
        var first = BiomeSet.Of(4, 200, 21);
        var second = BiomeSet.Empty.With(21).With(4).With(200).With(4);

        Assert.Equal(first, second);
        Assert.Equal(first.GetHashCode(), second.GetHashCode());
    }

    [Fact]
    public void Without_RemovesOnlyThatBiome()
    {
        var set = BiomeSet.Of(4, 200, 21).Without(200).Without(7);

        Assert.Equal([4, 21], set.ToArray());
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(256)]
    public void With_BiomeOutOfRange_Throws(int biome)
        => Assert.Throws<ArgumentOutOfRangeException>(() => BiomeSet.Empty.With(biome));

    [Fact]
    public void Json_RoundTripsAsAnArrayOfIds()
    {
        var set = BiomeSet.Of(1, 129, 14);

        var json = JsonSerializer.Serialize(set);
        var restored = JsonSerializer.Deserialize<BiomeSet>(json);

        Assert.Equal("[1,14,129]", json);
        Assert.Equal(set, restored);
    }

    [Fact]
    public void Json_BiomeOutOfRange_Throws()
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<BiomeSet>("[1,300]"));
}

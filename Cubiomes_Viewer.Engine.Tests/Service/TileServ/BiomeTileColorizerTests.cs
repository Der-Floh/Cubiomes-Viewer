using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.TileServ;

public sealed class BiomeTileColorizerTests
{
    private const int PlainsBiomeId = 1;

    private readonly BiomeTileColorizer _colorizer = new();
    private readonly BiomeCatalog _catalog = new();

    [Fact]
    public void Colorize_KnownBiome_WritesOpaqueBgraPixel()
    {
        var palette = _catalog.DefaultPalette;
        var expected = palette[PlainsBiomeId];
        var pixels = new byte[BiomeTileColorizer.BytesPerPixel];

        _colorizer.Colorize([PlainsBiomeId], palette, pixels);

        Assert.Equal([expected.Blue, expected.Green, expected.Red, byte.MaxValue], pixels);
    }

    [Fact]
    public void Colorize_MissingBiome_WritesTransparentPixel()
    {
        var pixels = new byte[] { 1, 2, 3, 4 };

        _colorizer.Colorize([Constants.NoBiomeTileValue], _catalog.DefaultPalette, pixels);

        Assert.Equal(new byte[BiomeTileColorizer.BytesPerPixel], pixels);
    }

    [Fact]
    public void Colorize_PixelBufferTooSmall_ThrowsArgumentException()
        => Assert.Throws<ArgumentException>(() => _colorizer.Colorize([PlainsBiomeId, PlainsBiomeId], _catalog.DefaultPalette, new byte[BiomeTileColorizer.BytesPerPixel]));

    [Fact]
    public void GetName_Plains_ReturnsResourceName()
        => Assert.Equal("plains", _catalog.GetName(McVersion.V1_21_3, PlainsBiomeId));

    [Fact]
    public void FromRgb_WrongLength_ThrowsArgumentException()
        => Assert.Throws<ArgumentException>(() => BiomePalette.FromRgb(new byte[Constants.BiomeColorChannels]));
}

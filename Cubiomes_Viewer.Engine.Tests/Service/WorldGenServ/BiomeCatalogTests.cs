using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.WorldGenServ;

public sealed class BiomeCatalogTests
{
    private const int PlainsBiomeId = 1;

    [Fact]
    public void GetName_Plains_ReturnsResourceName()
        => Assert.Equal("plains", new BiomeCatalog().GetName(McVersion.V1_21_3, PlainsBiomeId));

    [Fact]
    public void FromRgb_WrongLength_ThrowsArgumentException()
        => Assert.Throws<ArgumentException>(() => BiomePalette.FromRgb(new byte[Constants.BiomeColorChannels]));
}

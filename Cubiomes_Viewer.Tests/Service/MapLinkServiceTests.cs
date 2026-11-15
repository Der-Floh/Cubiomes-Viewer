using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.MapLinkServ;

namespace Cubiomes_Viewer.Tests.Service;

public sealed class MapLinkServiceTests
{
    private const string LinkBase = RecordingAppAddressService.TestLinkBase;
    private static readonly MapLink _open = new(1, McVersion.V1_20_6, Dimension.Overworld, true, new BlockPos(5, 6), 8, LayerMode.Temperature, 64, 2);

    private readonly MapLinkService _links = new(new VersionCatalog(), new SeedParser());

    [Fact]
    public void Format_View_WritesTheShownValuesAndLeavesDefaultsOut()
    {
        var link = new MapLink(262, McVersion.V1_21_3, Dimension.Nether, false, new BlockPos(100, -50), 4, LayerMode.Biomes, ViewSettings.DefaultSampleHeight, 0);

        Assert.Equal($"{LinkBase}?seed=262&version=1.21.3&dimension=nether&x=100&z=-50&scale=4", _links.Format(link, LinkBase));
    }

    [Fact]
    public void Format_ValuesOtherThanTheDefaults_WritesThemAfterThePlace()
    {
        var link = new MapLink(-5, McVersion.V1_21_WinterDrop, Dimension.Overworld, true, new BlockPos(0, 0), 1.0 / 32, LayerMode.Height, 70, 3);

        Assert.Equal($"{LinkBase}?seed=-5&version=1.21.4&dimension=overworld&x=0&z=0&scale=0.03125&largebiomes=1&layer=height&y=70&octaves=3", _links.Format(link, LinkBase));
    }

    [Theory]
    [InlineData(262, McVersion.V1_21_3, Dimension.Nether, false, 100, -50, 4, LayerMode.Biomes, 256, 0)]
    [InlineData(-4_172_144_997_902_289_642, McVersion.V1_21_WinterDrop, Dimension.End, false, -2_000_000, 12, 0.25, LayerMode.Height, 256, 0)]
    [InlineData(42, McVersion.Beta1_7, Dimension.Overworld, true, 7, 7, 1.0 / 32, LayerMode.Temperature, -64, 3)]
    public void TryParse_FormattedLink_ReadsTheSameView(long seed, McVersion version, Dimension dimension, bool largeBiomes, int x, int z, double blocksPerPixel, LayerMode layer, int y, int octaves)
    {
        var link = new MapLink(seed, version, dimension, largeBiomes, new BlockPos(x, z), blocksPerPixel, layer, y, octaves);

        Assert.True(_links.TryParse(_links.Format(link, LinkBase), _open, out var read, out var problem));

        Assert.Equal(link, read);
        Assert.Null(problem);
    }

    [Theory]
    [InlineData("?seed=262&x=10")]
    [InlineData("seed=262&x=10")]
    [InlineData("  https://example.test/viewer/?seed=262&x=10#later  ")]
    public void TryParse_QueryWithOrWithoutAddress_IsALink(string text)
    {
        Assert.True(_links.TryParse(text, _open, out var link, out _));

        Assert.Equal(262, link.Seed);
        Assert.Equal(new BlockPos(10, _open.Center.Z), link.Center);
    }

    [Theory]
    [InlineData("hello world")]
    [InlineData("x=5")]
    [InlineData("262")]
    [InlineData("")]
    public void TryParse_TextWithoutSeed_IsNoLink(string text)
    {
        Assert.False(_links.TryParse(text, _open, out var link, out var problem));

        Assert.Null(link);
        Assert.Null(problem);
    }

    [Theory]
    [InlineData("https://example.test/viewer/")]
    [InlineData("https://example.test/viewer/?x=5&seed=")]
    [InlineData("?dimension=end")]
    public void TryParse_AddressWithoutSeed_IsNoLinkAndSaysSo(string text)
    {
        Assert.False(_links.TryParse(text, _open, out _, out var problem));

        Assert.Equal("The link has no seed.", problem);
    }

    [Fact]
    public void TryParse_OnlyASeed_KeepsThePlaceOfTheOpenViewAndTakesTheOtherDefaults()
    {
        Assert.True(_links.TryParse("?seed=262", _open, out var link, out var problem));

        Assert.Equal(new MapLink(262, _open.Version, _open.Dimension, false, _open.Center, _open.BlocksPerPixel, LayerMode.Biomes, ViewSettings.DefaultSampleHeight, 0), link);
        Assert.Null(problem);
    }

    [Fact]
    public void TryParse_UnreadableValues_KeepThoseOfTheOpenViewAndAreNamedOnce()
    {
        Assert.True(_links.TryParse("?seed=262&version=1.99&scale=-2&layer=nope&dimension=nether", _open, out var link, out var problem));

        Assert.Equal(_open with { Seed = 262, Dimension = Dimension.Nether, LargeBiomes = false, SampleHeight = ViewSettings.DefaultSampleHeight, Octaves = 0 }, link);
        Assert.Equal("The link's version, scale and layer could not be read and stay as they are.", problem);
    }

    [Fact]
    public void TryParse_TextSeed_IsHashedLikeTheSeedBox()
    {
        Assert.True(_links.TryParse("?seed=hello+world", _open, out var link, out _));

        Assert.Equal(new SeedParser().Parse("hello world").Seed, link.Seed);
    }

    [Fact]
    public void TryParse_ValuesBeyondTheirLimits_AreClamped()
    {
        Assert.True(_links.TryParse("?seed=1&x=1e12&z=-1e12&scale=100000&y=999", _open, out var link, out var problem));

        Assert.Equal(new BlockPos(30_000_000, -30_000_000), link.Center);
        Assert.Equal(4096, link.BlocksPerPixel);
        Assert.Equal(320, link.SampleHeight);
        Assert.Null(problem);
    }

    [Fact]
    public void TryParse_NamesInAnyCaseOrTwice_KeepTheFirstValue()
    {
        Assert.True(_links.TryParse("?SEED=1&seed=2&Dimension=END&largebiomes=TRUE", _open, out var link, out _));

        Assert.Equal(1, link.Seed);
        Assert.Equal(Dimension.End, link.Dimension);
        Assert.True(link.LargeBiomes);
    }
}

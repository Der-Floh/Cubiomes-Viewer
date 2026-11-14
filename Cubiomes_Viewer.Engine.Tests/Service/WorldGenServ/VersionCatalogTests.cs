using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.WorldGenServ;

public sealed class VersionCatalogTests
{
    private readonly VersionCatalog _catalog = new();

    [Fact]
    public void TryGetVersion_NameOfEveryVersion_FindsThatVersion()
    {
        foreach (var version in _catalog.Versions)
        {
            Assert.True(_catalog.TryGetVersion(_catalog.GetName(version), out var found), $"{version} was not found by its name.");
            Assert.Equal(version, found);
        }
    }

    [Fact]
    public void TryGetVersion_OtherCaseWithSpacesAround_FindsTheVersion()
    {
        Assert.True(_catalog.TryGetVersion(" beta 1.7 ", out var found));
        Assert.Equal(McVersion.Beta1_7, found);
    }

    [Fact]
    public void TryGetVersion_UnknownName_FindsNothing() => Assert.False(_catalog.TryGetVersion("1.99", out _));
}

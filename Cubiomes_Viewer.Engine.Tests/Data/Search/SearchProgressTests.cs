using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Tests.Data.Search;

public sealed class SearchProgressTests
{
    [Fact]
    public void Share_WhilePreparing_IsThePreparedShare()
        => Assert.Equal(0.25, new SearchProgress(500, null, 0, null, Preparing: 0.25, Total: 1000).Share);

    [Theory]
    [InlineData(0, 1000, 0)]
    [InlineData(250, 1000, 0.25)]
    [InlineData(1000, 1000, 1)]
    [InlineData(1200, 1000, 1)]
    public void Share_OfASourceWithAnEnd_IsTheShareScanned(long scanned, long total, double expected)
        => Assert.Equal(expected, new SearchProgress((UInt128)scanned, null, 0, null, Total: (UInt128)total).Share);

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Share_WithoutATotal_IsUnknown(bool isEmpty)
        => Assert.Null(new SearchProgress(10, null, 0, null, Total: isEmpty ? UInt128.Zero : null).Share);
}

using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.TileServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.TileServ;

public sealed class LruCacheTests
{
    private const int TileBytes = 16;

    private static LruCache<TileRequest, TileData> CreateCache(long budgetBytes) => new(budgetBytes, static tile => tile.ByteSize);

    private static void Add(LruCache<TileRequest, TileData> cache, TileData tile) => cache.Add(tile.Request, tile);

    private static TileData CreateTile(int x)
    {
        var world = new WorldKey(1, McVersion.V1_21_3, Dimension.Overworld, GeneratorFlags.None);
        var request = new TileRequest(world, LayerMode.Biomes, 64, new TileKey(4, x, 0));
        return new TileData { Request = request, Samples = 4, Biomes = new byte[TileBytes] };
    }

    [Fact]
    public void Add_WithinBudget_KeepsEveryTile()
    {
        var cache = CreateCache(TileBytes * 3);
        var tiles = Enumerable.Range(0, 3).Select(CreateTile).ToArray();

        foreach (var tile in tiles)
            cache.Add(tile.Request, tile);

        Assert.Equal(3, cache.Count);
        Assert.Equal(TileBytes * 3, cache.UsedSize);
        Assert.All(tiles, tile => Assert.True(cache.TryGet(tile.Request, out _)));
    }

    [Fact]
    public void Add_OverBudget_EvictsLeastRecentlyUsedTile()
    {
        var cache = CreateCache(TileBytes * 2);
        var first = CreateTile(0);
        var second = CreateTile(1);
        cache.Add(first.Request, first);
        cache.Add(second.Request, second);
        Assert.True(cache.TryGet(first.Request, out _));

        Add(cache, CreateTile(2));

        Assert.True(cache.Contains(first.Request));
        Assert.False(cache.Contains(second.Request));
        Assert.Equal(2, cache.Count);
    }

    [Fact]
    public void Add_TileLargerThanBudget_KeepsOnlyThatTile()
    {
        var cache = CreateCache(TileBytes / 2);
        Add(cache, CreateTile(0));
        var latest = CreateTile(1);

        Add(cache, latest);

        Assert.Equal(1, cache.Count);
        Assert.True(cache.Contains(latest.Request));
    }

    [Fact]
    public void Add_SameRequestTwice_ReplacesWithoutDoubleCounting()
    {
        var cache = CreateCache(TileBytes * 4);

        Add(cache, CreateTile(0));
        Add(cache, CreateTile(0));

        Assert.Equal(1, cache.Count);
        Assert.Equal(TileBytes, cache.UsedSize);
    }

    [Fact]
    public void Budget_Lowered_EvictsDownToTheNewBudget()
    {
        var cache = CreateCache(TileBytes * 4);
        for (var x = 0; x < 4; x++)
            Add(cache, CreateTile(x));

        cache.Budget = TileBytes;

        Assert.Equal(1, cache.Count);
    }
}

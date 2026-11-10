using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>
/// The tunables of the tile pyramid for one kind of generator: tile size, available scales and zoom limits.
/// </summary>
public sealed class TileMetrics
{
    public const double MinBlocksPerPixel = 1.0 / 32;

    private const double SharpSamplePixels = 1;
    private const double EconomySamplePixels = 2;

    /// <summary>Samples per tile side.</summary>
    public required int TileSamples { get; init; }

    /// <summary>Blocks per sample of each level, finest first.</summary>
    public required IReadOnlyList<int> Scales { get; init; }

    /// <summary>A level is used while its samples are at least this many pixels wide.</summary>
    public required double MinSamplePixels { get; init; }

    public required double MaxBlocksPerPixel { get; init; }

    /// <summary>
    /// Noise-based generators (1.18+, Beta 1.7, Nether, End) sample any scale but are costly per sample,
    /// so single-threaded WASM switches to the coarser level earlier.
    /// </summary>
    public static TileMetrics Noise { get; } = new()
    {
        TileSamples = 128,
        Scales = [1, 4, 16, 64, 256, 1024, 4096],
        MinSamplePixels = OperatingSystem.IsBrowser() ? EconomySamplePixels : SharpSamplePixels,
        MaxBlocksPerPixel = 4096,
    };

    /// <summary>The layered Overworld generator (Beta 1.8 to 1.17) is cheap per sample but stops at 1:256.</summary>
    public static TileMetrics Layered { get; } = new()
    {
        TileSamples = 256,
        Scales = [1, 4, 16, 64, 256],
        MinSamplePixels = SharpSamplePixels,
        MaxBlocksPerPixel = 512,
    };

    public static TileMetrics For(WorldKey world)
        => world.Dimension == Dimension.Overworld && world.Version is >= McVersion.Beta1_8 and <= McVersion.V1_17_1
            ? Layered
            : Noise;
}

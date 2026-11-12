using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Export;

/// <summary>
/// What an image export shows: one layer of one or more worlds over an area of blocks, one pixel per sample at
/// <see cref="Scale"/> blocks. Several worlds, or a <see cref="TileSize"/>, make a ZIP archive of PNG images.
/// </summary>
public sealed record MapExportRequest
{
    public required IReadOnlyList<WorldKey> Worlds { get; init; }
    public LayerMode Layer { get; init; }
    public int SampleHeight { get; init; }
    public int Octaves { get; init; }
    public TileStyle Style { get; init; }

    /// <summary>The blocks to cover; a sample is in the image when its first block is.</summary>
    public required BlockArea Area { get; init; }

    /// <summary>Blocks per pixel along each side.</summary>
    public required int Scale { get; init; }

    /// <summary>The side of the tiles each image is cut into, in pixels; <see langword="null"/> keeps every image whole.</summary>
    public int? TileSize { get; init; }

    public bool IsArchive => Worlds.Count > 1 || TileSize is not null;

    public MapImageLayout GetLayout()
    {
        var sampleX = FloorDivide(Area.X, Scale);
        var sampleZ = FloorDivide(Area.Z, Scale);
        return new MapImageLayout(sampleX, sampleZ, FloorDivide(Area.Right - 1, Scale) - sampleX + 1, FloorDivide(Area.Bottom - 1, Scale) - sampleZ + 1);
    }

    private static int FloorDivide(int value, int divisor) => (int)Math.Floor(value / (double)divisor);
}

namespace Cubiomes_Viewer.Engine.Data.Export;

/// <summary>The samples an exported image covers: one pixel each, from (<see cref="SampleX"/>, <see cref="SampleZ"/>) on.</summary>
public readonly record struct MapImageLayout(int SampleX, int SampleZ, int Width, int Height)
{
    public long PixelCount => (long)Width * Height;

    /// <summary>The number of tiles of a side length that cover the image, edge tiles cut to the image.</summary>
    public (int Columns, int Rows) CountTiles(int tileSize) => ((Width + tileSize - 1) / tileSize, (Height + tileSize - 1) / tileSize);
}

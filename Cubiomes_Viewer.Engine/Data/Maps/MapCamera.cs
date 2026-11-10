namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>Where the map looks: the block at the centre of the view and the zoom in blocks per pixel.</summary>
public readonly record struct MapCamera(double CenterX, double CenterZ, double BlocksPerPixel)
{
    public const double DefaultBlocksPerPixel = 4;

    public static MapCamera Default { get; } = new(0, 0, DefaultBlocksPerPixel);
}

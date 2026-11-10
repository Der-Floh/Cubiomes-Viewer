namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>A camera together with the pixel size of the view it is shown in.</summary>
public readonly record struct MapViewport(MapCamera Camera, double Width, double Height)
{
    public double BlocksPerPixel => Camera.BlocksPerPixel;
    public double Left => Camera.CenterX - Width / 2 * BlocksPerPixel;
    public double Top => Camera.CenterZ - Height / 2 * BlocksPerPixel;
    public double Right => Camera.CenterX + Width / 2 * BlocksPerPixel;
    public double Bottom => Camera.CenterZ + Height / 2 * BlocksPerPixel;
    public bool IsEmpty => Width <= 0 || Height <= 0;

    public (double X, double Z) ToWorld(double pixelX, double pixelY)
        => (Left + pixelX * BlocksPerPixel, Top + pixelY * BlocksPerPixel);

    public (double X, double Y) ToScreen(double blockX, double blockZ)
        => ((blockX - Left) / BlocksPerPixel, (blockZ - Top) / BlocksPerPixel);
}

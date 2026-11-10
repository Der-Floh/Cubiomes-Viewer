namespace Cubiomes_Viewer.Data;

/// <summary>Something that shows the map and can turn its current picture into an image file.</summary>
public interface IMapImageSource
{
    /// <summary>Renders the map as it is shown right now and returns it as a PNG file.</summary>
    byte[] CapturePng();
}

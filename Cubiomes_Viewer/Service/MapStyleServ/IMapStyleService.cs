using Avalonia;
using Avalonia.Media;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Service.MapStyleServ;

/// <summary>Provides the icons and colours of the map overlays from the application resources and draws markers with them.</summary>
public interface IMapStyleService
{
    /// <summary>The side length of the square a pin is drawn in, in device-independent pixels; its tip is near the bottom.</summary>
    double PinSize { get; }

    /// <summary>How much larger or smaller than normal markers and pins are drawn.</summary>
    double MarkerScale { get; set; }

    MapMarker Spawn { get; }
    MapMarker Stronghold { get; }

    /// <summary>A pin with nothing in its head.</summary>
    PinLook PlainPin { get; }
    Color SlimeChunkColor { get; }
    Color MeasureColor { get; }
    Color OutlineColor { get; }
    Color SelectionColor { get; }

    MapMarker GetStructureMarker(StructureType type);

    /// <summary>The badge of a biome: its glyph on the biome's default map colour, in white or dark, whichever stands out more.</summary>
    MapMarker GetBiomeMarker(int biome);

    /// <summary>The badge a dimension is picked by.</summary>
    MapMarker GetDimensionMarker(Dimension dimension);

    /// <summary>A pin showing the glyph of the named marker; the plain pin when there is no name or no such marker.</summary>
    PinLook GetPinLook(string? icon);

    /// <summary>
    /// The side length of a marker drawn <paramref name="magnification"/> times its size on these device pixels, in
    /// device-independent pixels; its glyph needs whole device pixels, so the side differs a little from screen to screen.
    /// </summary>
    double GetMarkerSize(PixelGrid pixels, double magnification = 1);

    /// <summary>Draws a marker centred on a point, <paramref name="magnification"/> times its size, with its edges on device pixels.</summary>
    void DrawMarker(DrawingContext context, MapMarker marker, Point center, PixelGrid pixels, double magnification = 1);

    /// <summary>Draws a pin with its tip on a point, <paramref name="magnification"/> times its size.</summary>
    void DrawPin(DrawingContext context, PinLook look, Point tip, PixelGrid pixels, double magnification = 1);

    /// <summary>The area a pin with its tip on a point covers.</summary>
    Rect GetPinBounds(Point tip, double magnification = 1);
}

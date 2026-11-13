using Avalonia.Media;

using CommunityToolkit.Mvvm.ComponentModel;

namespace Cubiomes_Viewer.Service.MapStyleServ;

/// <summary>How a point of interest looks on the map: an SVG glyph on a filled badge. The glyph changes with the marker style.</summary>
public sealed partial class MapMarker : ObservableObject
{
    [ObservableProperty]
    public partial IImage Glyph { get; set; }

    public IBrush Fill { get; }

    public MapMarker(IImage glyph, IBrush fill)
    {
        Glyph = glyph;
        Fill = fill;
    }
}

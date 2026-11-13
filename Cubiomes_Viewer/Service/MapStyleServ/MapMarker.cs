using Avalonia.Media;

namespace Cubiomes_Viewer.Service.MapStyleServ;

/// <summary>How a point of interest looks on the map: an SVG glyph on a filled badge.</summary>
public sealed record MapMarker(IImage Glyph, IBrush Fill);

using Avalonia.Media;

namespace Cubiomes_Viewer.Service.MapStyleServ;

/// <summary>How a point of interest looks on the map: a glyph on a filled badge.</summary>
public sealed record MapMarker(Geometry Glyph, IBrush Fill);

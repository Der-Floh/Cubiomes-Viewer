using Cubiomes_Viewer.Engine.Data.Maps;

namespace Cubiomes_Viewer.Data;

/// <summary>A move of the map to a camera: a glide there, or a jump straight to it, as for a move to another world.</summary>
public readonly record struct MapNavigation(MapCamera Target, bool Animate);

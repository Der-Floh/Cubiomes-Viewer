using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Data;

/// <summary>The part of the world an analysis looks at: what the map showed when it started, around its centre, in its dimension.</summary>
public sealed record AnalysisView(SearchArea Area, BlockPos Reference, Dimension Dimension)
{
    /// <summary>The same blocks as an area around the world origin, which is where conditions that are not relative to another look around.</summary>
    public SearchArea AroundOrigin => SearchArea.Rectangle(Reference.X + Area.MinX, Reference.Z + Area.MinZ, Reference.X + Area.MaxX, Reference.Z + Area.MaxZ);

    public bool Contains(BlockPos position) => Area.Contains(Reference, position);
}

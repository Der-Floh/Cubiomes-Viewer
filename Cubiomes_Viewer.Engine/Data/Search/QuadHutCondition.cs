using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>
/// Four structures of one type generate close enough together that one point is in range of all of them, and the
/// middle of the four lies in the area. Swamp huts make the farms; desert pyramids, jungle temples and igloos are
/// placed the same way.
/// </summary>
public sealed record QuadHutCondition : SearchCondition
{
    public StructureType Type { get; init; } = StructureType.SwampHut;

    /// <summary>The worst class of constellation that counts.</summary>
    public QuadQuality Quality { get; init; } = QuadQuality.Barely;
}

using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.Data;

/// <summary>The choices every editor of quads offers: which huts and how close together.</summary>
public static class QuadChoices
{
    public static IReadOnlyList<StructureType> Types { get; } = [StructureType.SwampHut, StructureType.DesertPyramid, StructureType.JungleTemple, StructureType.Igloo];

    public static IReadOnlyList<ComboChoice<QuadQuality>> Qualities { get; } =
    [
        new() { Display = "An ideal constellation", Value = QuadQuality.Ideal },
        new() { Display = "A classic constellation", Value = QuadQuality.Classic },
        new() { Display = "Room for a 43-block drop", Value = QuadQuality.Normal },
        new() { Display = "All four in range of one point", Value = QuadQuality.Barely },
    ];

    public static IEnumerable<ComboChoice<StructureType>> CreateTypeChoices(IStructureCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        return Types.Select(type => new ComboChoice<StructureType> { Display = catalog.GetInfo(type).Name, Value = type });
    }
}

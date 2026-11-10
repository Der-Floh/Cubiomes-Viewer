using System.Collections.Frozen;

using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class StructureCatalog : IStructureCatalog
{
    private static readonly StructureInfo[] _structures =
    [
        new(StructureType.Village, "Village", 16),
        new(StructureType.Outpost, "Pillager outpost", 32),
        new(StructureType.DesertPyramid, "Desert pyramid", 16),
        new(StructureType.JungleTemple, "Jungle temple", 16),
        new(StructureType.SwampHut, "Swamp hut", 16),
        new(StructureType.Igloo, "Igloo", 16),
        new(StructureType.Monument, "Ocean monument", 16),
        new(StructureType.Mansion, "Woodland mansion", 64),
        new(StructureType.OceanRuin, "Ocean ruin", 8),
        new(StructureType.Shipwreck, "Shipwreck", 8),
        new(StructureType.Treasure, "Buried treasure", 8),
        new(StructureType.RuinedPortal, "Ruined portal", 16),
        new(StructureType.AncientCity, "Ancient city", 16),
        new(StructureType.TrailRuins, "Trail ruins", 16),
        new(StructureType.TrialChambers, "Trial chambers", 16),
        new(StructureType.Mineshaft, "Mineshaft", 4),
        new(StructureType.DesertWell, "Desert well", 8),
        new(StructureType.Geode, "Amethyst geode", 2),
        new(StructureType.Fortress, "Nether fortress", 8),
        new(StructureType.Bastion, "Bastion remnant", 8),
        new(StructureType.RuinedPortalNether, "Ruined portal", 8),
        new(StructureType.EndCity, "End city", 8),
        new(StructureType.EndGateway, "End gateway", 8),
    ];

    private static readonly FrozenDictionary<StructureType, StructureInfo> _byType = _structures.ToFrozenDictionary(structure => structure.Type);

    public IReadOnlyList<StructureInfo> Structures => _structures;

    private readonly IStructureFinder _structureFinder;

    public StructureCatalog(IStructureFinder structureFinder)
    {
        _structureFinder = structureFinder;
    }

    public StructureInfo GetInfo(StructureType type)
        => _byType.TryGetValue(type, out var info)
            ? info
            : throw new ArgumentOutOfRangeException(nameof(type), type, "The map cannot show this structure type.");

    public IReadOnlyList<StructureInfo> GetAvailable(McVersion version, Dimension dimension)
        => [.. _structures.Where(structure => _structureFinder.TryGetPlacement(structure.Type, version, out var placement) && placement.Dimension == dimension)];
}

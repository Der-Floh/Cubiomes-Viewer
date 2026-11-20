using System.Collections.Frozen;

using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class StructureCatalog : IStructureCatalog
{
    private static readonly StructureInfo[] _structures =
    [
        new(StructureType.Village, "Village", 64),
        new(StructureType.Outpost, "Pillager outpost", 128),
        new(StructureType.DesertPyramid, "Desert pyramid", 64),
        new(StructureType.JungleTemple, "Jungle temple", 64),
        new(StructureType.SwampHut, "Swamp hut", 64),
        new(StructureType.Igloo, "Igloo", 64),
        new(StructureType.Monument, "Ocean monument", 64),
        new(StructureType.Mansion, "Woodland mansion", 256),
        new(StructureType.OceanRuin, "Ocean ruin", 32),
        new(StructureType.Shipwreck, "Shipwreck", 32),
        new(StructureType.Treasure, "Buried treasure", 32),
        new(StructureType.RuinedPortal, "Ruined portal", 64),
        new(StructureType.AncientCity, "Ancient city", 64),
        new(StructureType.TrailRuins, "Trail ruins", 64),
        new(StructureType.TrialChambers, "Trial chambers", 64),
        new(StructureType.AbandonedCamp, "Abandoned camp", 64),
        new(StructureType.Mineshaft, "Mineshaft", 16),
        new(StructureType.DesertWell, "Desert well", 32),
        new(StructureType.Geode, "Amethyst geode", 8),
        new(StructureType.Fortress, "Nether fortress", 32),
        new(StructureType.Bastion, "Bastion remnant", 32),
        new(StructureType.RuinedPortalNether, "Ruined portal", 32),
        new(StructureType.NetherFossil, "Nether fossil", 2),
        new(StructureType.EndCity, "End city", 32),
        new(StructureType.EndGateway, "End gateway", 32),
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

    public IReadOnlyList<StructureInfo> GetWithBiomeRule(McVersion version, Dimension dimension)
        => [.. GetAvailable(version, dimension).Where(structure => _structureFinder.TryGetViableBiomes(structure.Type, version, out _))];
}

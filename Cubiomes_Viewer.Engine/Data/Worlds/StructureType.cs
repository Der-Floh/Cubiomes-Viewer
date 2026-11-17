namespace Cubiomes_Viewer.Engine.Data.Worlds;

/// <summary>
/// Structure kinds known to the native generator. The numeric values mirror the
/// <c>StructureType</c> enum of the pinned cubiomes revision and must be kept in the same order.
/// </summary>
public enum StructureType
{
    Feature = 0,
    DesertPyramid,
    JungleTemple,
    SwampHut,
    Igloo,
    Village,
    OceanRuin,
    Shipwreck,
    Monument,
    Mansion,
    Outpost,
    RuinedPortal,
    RuinedPortalNether,
    AncientCity,
    Treasure,
    Mineshaft,
    DesertWell,
    Geode,
    Fortress,
    Bastion,
    NetherFossil,
    EndCity,
    EndGateway,
    EndIsland,
    TrailRuins,
    TrialChambers,
    AbandonedCamp,

    /// <summary>Placed in rings around the world's centre rather than in regions; only its details are read by type.</summary>
    Stronghold,
}

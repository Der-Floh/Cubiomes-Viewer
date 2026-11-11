using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Settings;

public sealed class MapSettings
{
    public const int MinCacheMegabytes = 32;
    public const int MaxCacheMegabytes = 4096;
    public const int DefaultCacheMegabytes = 256;

    public int CacheMegabytes { get; set; } = DefaultCacheMegabytes;
    public bool SmoothMotion { get; set; } = true;
    public bool ShowGrid { get; set; } = true;
    public bool ShowSlimeChunks { get; set; }
    public bool ShowSpawn { get; set; } = true;
    public bool ShowStrongholds { get; set; } = true;
    public bool ShowGatewayLinks { get; set; } = true;

    /// <summary>The structure types marked on the map, across all dimensions.</summary>
    public List<StructureType> VisibleStructures { get; set; } =
    [
        StructureType.Village,
        StructureType.Outpost,
        StructureType.DesertPyramid,
        StructureType.JungleTemple,
        StructureType.SwampHut,
        StructureType.Igloo,
        StructureType.Monument,
        StructureType.Mansion,
        StructureType.AncientCity,
        StructureType.TrialChambers,
        StructureType.Fortress,
        StructureType.Bastion,
        StructureType.EndCity,
    ];
}

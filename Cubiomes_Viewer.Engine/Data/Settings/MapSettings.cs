using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Settings;

public sealed class MapSettings
{
    public const int MinCacheMegabytes = 32;
    public const int MaxCacheMegabytes = 4096;
    public const int DefaultCacheMegabytes = 256;
    public const int DefaultGridSpacing = 16;
    public const int DefaultGridMultiplier = 2;

    public int CacheMegabytes { get; set; } = DefaultCacheMegabytes;
    public bool SmoothMotion { get; set; } = true;
    public bool ShowGrid { get; set; } = true;

    /// <summary>The blocks between grid lines when zoomed in; zoomed out, the spacing grows by <see cref="GridMultiplier"/> until lines are apart.</summary>
    public int GridSpacing { get; set; } = DefaultGridSpacing;
    public int GridMultiplier { get; set; } = DefaultGridMultiplier;
    public MarkerSize MarkerSize { get; set; } = MarkerSize.Normal;

    /// <summary>Zoom limits that differ from the defaults of their structure types.</summary>
    public List<StructureZoomLimit> ZoomLimits { get; set; } = [];
    public bool ShowSlimeChunks { get; set; }
    public bool ShowSpawn { get; set; } = true;
    public bool ShowStrongholds { get; set; } = true;
    public bool ShowGatewayLinks { get; set; } = true;
    public HeightStyle HeightStyle { get; set; } = HeightStyle.Shaded;

    /// <summary>The name of the colour map biomes are painted with; the default colours when it is <see langword="null"/>.</summary>
    public string? ColorMap { get; set; }

    /// <summary>The structure type whose biomes the structure potential layer highlights.</summary>
    public StructureType PotentialStructure { get; set; } = StructureType.Village;

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

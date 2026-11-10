using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Settings;

/// <summary>The world and map position that were open last, restored at the next start.</summary>
public sealed class ViewSettings
{
    public const int DefaultSampleHeight = 256;

    public long Seed { get; set; }
    public McVersion Version { get; set; } = McVersion.V1_21_3;
    public bool LargeBiomes { get; set; }
    public Dimension Dimension { get; set; } = Dimension.Overworld;
    public int SampleHeight { get; set; } = DefaultSampleHeight;
    public double CenterX { get; set; }
    public double CenterZ { get; set; }
    public double BlocksPerPixel { get; set; } = MapCamera.DefaultBlocksPerPixel;
    public List<long> RecentSeeds { get; set; } = [];
}

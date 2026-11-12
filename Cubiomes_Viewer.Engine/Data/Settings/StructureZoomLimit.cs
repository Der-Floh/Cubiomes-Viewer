using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Settings;

/// <summary>How far out a structure type stays marked: up to this many blocks per pixel, in place of its default.</summary>
public sealed class StructureZoomLimit
{
    public StructureType Type { get; set; }
    public double MaxBlocksPerPixel { get; set; }
}

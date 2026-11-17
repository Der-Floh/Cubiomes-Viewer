using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.LayerServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class StructureHeightService : IStructureHeightService
{
    private const int AncientCityFloorY = -51;
    private const int MonumentFloorY = 39;
    private const int BastionFloorY = 33;
    private const int LavaSeaY = 32;
    private const int FortressLowestY = 48;
    private const int FortressHighestY = 70;
    private const int TrailRuinsDepth = 15;
    private const int SurfaceScaleShift = 2;
    private const int SurfaceScale = 1 << SurfaceScaleShift;

    private readonly ILayerCatalog _layerCatalog;

    public StructureHeightService(ILayerCatalog layerCatalog)
    {
        _layerCatalog = layerCatalog;
    }

    public StructureHeight? Find(IWorldGenerator generator, Dimension dimension, StructureDetails details)
    {
        ArgumentNullException.ThrowIfNull(generator);
        ArgumentNullException.ThrowIfNull(details);

        return details.Type switch
        {
            StructureType.AncientCity => StructureHeight.At(AncientCityFloorY),
            StructureType.Monument => StructureHeight.At(MonumentFloorY),
            StructureType.Bastion => StructureHeight.At(BastionFloorY),
            StructureType.TrialChambers => details.Variant?.Height is { } floor ? StructureHeight.At(floor) : null,
            StructureType.Geode => details.Variant?.Height is { } middle ? StructureHeight.Around(middle) : null,
            StructureType.NetherFossil => FindNetherFossilHeight(details.Variant),
            StructureType.Fortress => FindFortressHeight(details.Pieces),
            StructureType.TrailRuins => EstimateSurface(generator, dimension, details, -TrailRuinsDepth),
            StructureType.DesertPyramid or StructureType.JungleTemple or StructureType.SwampHut or StructureType.Igloo or StructureType.Village
                or StructureType.OceanRuin or StructureType.Shipwreck or StructureType.Mansion or StructureType.Outpost or StructureType.Treasure
                or StructureType.DesertWell or StructureType.EndCity or StructureType.AbandonedCamp => EstimateSurface(generator, dimension, details, 0),
            StructureType.Feature or StructureType.RuinedPortal or StructureType.RuinedPortalNether or StructureType.Mineshaft
                or StructureType.EndGateway or StructureType.EndIsland => null,
            _ => throw new NotSupportedException($"Unsupported structure type: {details.Type}"),
        };
    }

    /// <summary>A nether fossil lies on the first floor below a height picked by chance, and only above the lava sea.</summary>
    private static StructureHeight? FindNetherFossilHeight(StructureVariant? variant)
        => variant?.Height is { } highest && highest > LavaSeaY ? StructureHeight.Between(LavaSeaY + 1, highest) : null;

    /// <summary>
    /// The game moves a whole fortress up or down by chance until it lies between Y 48 and 70, after the generator has placed its
    /// pieces, so the floor of the start piece is known only to lie in the range that move allows.
    /// </summary>
    private static StructureHeight? FindFortressHeight(StructurePiece[] pieces)
    {
        if (pieces.Length == 0)
            return null;

        var bottom = pieces.Min(piece => piece.Box.MinY);
        var height = pieces.Max(piece => piece.Box.MaxY) - bottom + 1;
        var highestBottom = Math.Max(FortressLowestY, FortressHighestY - height);
        var startAboveBottom = pieces[0].Box.MinY - bottom;
        return StructureHeight.Between(FortressLowestY + startAboveBottom, highestBottom + startAboveBottom);
    }

    private StructureHeight? EstimateSurface(IWorldGenerator generator, Dimension dimension, StructureDetails details, int offset)
    {
        if (!_layerCatalog.IsAvailable(LayerMode.Height, generator.Version, dimension))
            return null;

        var middle = GetMiddle(details);
        Span<short> surface = stackalloc short[1];
        generator.GenerateHeights(new BiomeArea(SurfaceScale, middle.X >> SurfaceScaleShift, middle.Z >> SurfaceScaleShift, 1, 1, 0), surface);
        var y = (int)Math.Round(surface[0] / (double)Constants.HeightUnit, MidpointRounding.AwayFromZero);
        return dimension == Dimension.End && y <= 0 ? null : StructureHeight.Around(y + offset);
    }

    /// <summary>The middle of the start piece, where the game reads the surface for most structures, or else the structure's position.</summary>
    private static BlockPos GetMiddle(StructureDetails details)
        => details.Variant?.Box is { } box ? new BlockPos((box.MinX + box.MaxX) / 2, (box.MinZ + box.MaxZ) / 2) : details.Position;
}

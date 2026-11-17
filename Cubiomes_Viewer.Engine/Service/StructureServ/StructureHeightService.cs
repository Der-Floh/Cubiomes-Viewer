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
    private const int SeaLevel = 63;
    private const int FortressLowestY = 48;
    private const int FortressHighestY = 70;
    private const int TrailRuinsDepth = 15;
    private const int OldDesertPyramidFloorY = 64;
    private const int DesertPyramidSide = 21;
    private const int DesertPyramidMaxSink = 2;
    private const int ChunkMask = ~15;
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
            StructureType.NetherFossil => FindFloor(generator, details) ?? FindNetherFossilRange(details.Variant),
            StructureType.Fortress => FindFortressHeight(details.Pieces),
            StructureType.EndCity or StructureType.Mansion => FindFloor(generator, details) ?? EstimateSurface(generator, dimension, details, 0),
            StructureType.Village => FindVillageHeight(generator, dimension, details),
            StructureType.DesertPyramid => FindDesertPyramidHeight(generator, dimension, details),
            StructureType.JungleTemple or StructureType.SwampHut => FindTempleHeight(generator, dimension, details),
            StructureType.TrailRuins => EstimateSurface(generator, dimension, details, -TrailRuinsDepth),
            StructureType.Igloo or StructureType.Outpost or StructureType.DesertWell or StructureType.AbandonedCamp
                => EstimateSurface(generator, dimension, details, 0),
            StructureType.OceanRuin or StructureType.Shipwreck or StructureType.Treasure => EstimateGround(generator, dimension, details),
            StructureType.Feature or StructureType.RuinedPortal or StructureType.RuinedPortalNether or StructureType.Mineshaft
                or StructureType.EndGateway or StructureType.EndIsland or StructureType.Stronghold => null,
            _ => throw new NotSupportedException($"Unsupported structure type: {details.Type}"),
        };
    }

    private static StructureHeight? FindFloor(IWorldGenerator generator, StructureDetails details)
        => generator.FindStructureFloor(details.Type, details.Position) is { } floor ? StructureHeight.At(floor) : null;

    /// <summary>A nether fossil lies on the first floor below a height picked by chance, and only above the lava sea.</summary>
    private static StructureHeight? FindNetherFossilRange(StructureVariant? variant)
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

    /// <summary>A village stands on the surface, water included, under the middle of its start piece.</summary>
    private StructureHeight? FindVillageHeight(IWorldGenerator generator, Dimension dimension, StructureDetails details)
    {
        if (!generator.HasExactTerrain || details.Variant?.Box is null)
            return EstimateSurface(generator, dimension, details, 0);

        var middle = GetMiddle(details);
        return StructureHeight.At(ReadSurface(generator, dimension, new BlockArea(middle.X, middle.Z, 1, 1))[0]);
    }

    /// <summary>
    /// Before 1.18 a desert pyramid always stands at Y 64. From 1.18 on it settles on the lowest ground under it and sinks up to two
    /// blocks deeper, by a chance that depends on which of its chunks the game decorates first.
    /// </summary>
    private StructureHeight? FindDesertPyramidHeight(IWorldGenerator generator, Dimension dimension, StructureDetails details)
    {
        if (generator.Version < McVersion.V1_18_2)
            return StructureHeight.At(OldDesertPyramidFloorY);
        if (!generator.HasExactTerrain)
            return EstimateSurface(generator, dimension, details, 0);

        var corner = new BlockPos(details.Position.X & ChunkMask, details.Position.Z & ChunkMask);
        var lowest = ReadSurface(generator, dimension, new BlockArea(corner.X, corner.Z, DesertPyramidSide, DesertPyramidSide)).Min();
        return StructureHeight.Between(lowest - DesertPyramidMaxSink, lowest);
    }

    /// <summary>
    /// Jungle temples and swamp huts stand on the average height of the ground under them, water included. The generator knows which
    /// way they face from 1.20 on; before that the average over their unturned footprint is close.
    /// </summary>
    private StructureHeight? FindTempleHeight(IWorldGenerator generator, Dimension dimension, StructureDetails details)
    {
        if (!generator.HasExactTerrain || details.Variant?.Box is not { } box)
            return EstimateSurface(generator, dimension, details, 0);

        var surface = ReadSurface(generator, dimension, new BlockArea(box.MinX, box.MinZ, box.SizeX, box.SizeZ));
        var average = surface.Sum(height => height) / surface.Length;
        return generator.Version >= McVersion.V1_20_6 ? StructureHeight.At(average) : StructureHeight.Around(average);
    }

    /// <summary>An estimate of the surface, water included, under the middle of a structure that stands on the ground.</summary>
    private StructureHeight? EstimateSurface(IWorldGenerator generator, Dimension dimension, StructureDetails details, int offset)
    {
        if (!generator.HasExactTerrain)
            return EstimateApproximately(generator, dimension, details, offset);

        var middle = GetMiddle(details);
        var surface = ReadSurface(generator, dimension, new BlockArea(middle.X, middle.Z, 1, 1))[0];
        return dimension == Dimension.End && surface <= 0 ? null : StructureHeight.Around(surface + offset);
    }

    /// <summary>An estimate of the ground under the middle of a structure on the sea floor, below any water.</summary>
    private StructureHeight? EstimateGround(IWorldGenerator generator, Dimension dimension, StructureDetails details)
    {
        if (!generator.HasExactTerrain)
            return EstimateApproximately(generator, dimension, details, 0);

        var middle = GetMiddle(details);
        Span<short> ground = stackalloc short[1];
        generator.GenerateSurface(new BlockArea(middle.X, middle.Z, 1, 1), ground);
        return StructureHeight.Around(ground[0]);
    }

    /// <summary>The approximate surface of the map's height layer, for versions whose exact terrain the generator does not build.</summary>
    private StructureHeight? EstimateApproximately(IWorldGenerator generator, Dimension dimension, StructureDetails details, int offset)
    {
        if (!_layerCatalog.IsAvailable(LayerMode.Height, generator.Version, dimension))
            return null;

        var middle = GetMiddle(details);
        Span<short> surface = stackalloc short[1];
        generator.GenerateHeights(new BiomeArea(SurfaceScale, middle.X >> SurfaceScaleShift, middle.Z >> SurfaceScaleShift, 1, 1, 0), surface);
        var y = (int)Math.Round(surface[0] / (double)Constants.HeightUnit, MidpointRounding.AwayFromZero);
        return dimension == Dimension.End && y <= 0 ? null : StructureHeight.Around(y + offset);
    }

    /// <summary>The surface the game's height maps see: the ground, or in the Overworld the sea where the ground lies below it.</summary>
    private static short[] ReadSurface(IWorldGenerator generator, Dimension dimension, BlockArea area)
    {
        var surface = new short[area.SizeX * area.SizeZ];
        generator.GenerateSurface(area, surface);
        if (dimension == Dimension.Overworld)
        {
            for (var index = 0; index < surface.Length; index++)
                surface[index] = Math.Max(surface[index], (short)SeaLevel);
        }

        return surface;
    }

    /// <summary>The middle of the start piece, where the game reads the surface for most structures, or else the structure's position.</summary>
    private static BlockPos GetMiddle(StructureDetails details)
        => details.Variant?.Box is { } box ? new BlockPos((box.MinX + box.MaxX) / 2, (box.MinZ + box.MaxZ) / 2) : details.Position;
}

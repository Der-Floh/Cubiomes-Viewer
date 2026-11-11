using System.Globalization;

using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Service.StructureDescriptionServ;

public sealed class StructureDescriptionService : IStructureDescriptionService
{
    private static readonly string[] _bastionTypes = ["Housing units", "Hoglin stables", "Treasure room", "Bridge"];

    private readonly IBiomeCatalog _biomeCatalog;

    public StructureDescriptionService(IBiomeCatalog biomeCatalog)
    {
        _biomeCatalog = biomeCatalog;
    }

    public IReadOnlyList<string> Describe(StructureDetails details, McVersion version)
    {
        ArgumentNullException.ThrowIfNull(details);

        List<string> lines = [];
        if (details.Variant is { } variant)
        {
            lines.AddRange(DescribeVariant(details.Type, variant, version));
            AddHeight(lines, details.Type, variant);
            AddSize(lines, details.Type, variant);
        }

        lines.AddRange(DescribePieces(details.Type, details.Pieces));
        return lines;
    }

    private IEnumerable<string> DescribeVariant(StructureType type, StructureVariant variant, McVersion version) => type switch
    {
        StructureType.Village => DescribeVillage(variant, version),
        StructureType.Bastion => DescribeBastion(variant),
        StructureType.RuinedPortal or StructureType.RuinedPortalNether => DescribeRuinedPortal(variant),
        StructureType.Igloo => [variant.HasBasement ? "Has a basement" : "No basement"],
        StructureType.Geode => [variant.IsCracked ? "Cracked open" : "Closed shell"],
        _ => [],
    };

    private IEnumerable<string> DescribeVillage(StructureVariant variant, McVersion version)
    {
        if (variant.Biome != StructureVariant.None)
            yield return $"{_biomeCatalog.GetDisplayName(version, variant.Biome)} village";
        if (variant.IsAbandoned)
            yield return "Abandoned (zombie village)";
    }

    private static IEnumerable<string> DescribeBastion(StructureVariant variant)
    {
        if (variant.Start >= 0 && variant.Start < _bastionTypes.Length)
            yield return _bastionTypes[variant.Start];
    }

    private static IEnumerable<string> DescribeRuinedPortal(StructureVariant variant)
    {
        if (variant.IsGiant)
            yield return "Giant portal";
        if (variant.IsUnderground)
            yield return "Buried underground";
        else if (variant.HasAirPocket)
            yield return "In an air pocket";
    }

    private static void AddHeight(List<string> lines, StructureType type, StructureVariant variant)
    {
        if (variant.Height is { } height)
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"{(type == StructureType.Geode ? "Around" : "At")} Y {height}"));
    }

    private static void AddSize(List<string> lines, StructureType type, StructureVariant variant)
    {
        if (variant.Box is not { } box)
            return;

        var label = HasFurtherPieces(type) ? "Start piece" : "Size";
        lines.Add($"{label}: {DescribeSize(box)} blocks");
    }

    /// <summary>A box one block high is a footprint: the generator does not tell how tall the structure is.</summary>
    private static string DescribeSize(BlockBox box) => box.SizeY > 1
        ? string.Create(CultureInfo.InvariantCulture, $"{box.SizeX} × {box.SizeY} × {box.SizeZ}")
        : string.Create(CultureInfo.InvariantCulture, $"{box.SizeX} × {box.SizeZ}");

    private static bool HasFurtherPieces(StructureType type)
        => type is StructureType.Village or StructureType.Bastion or StructureType.AncientCity or StructureType.TrialChambers;

    private static IEnumerable<string> DescribePieces(StructureType type, StructurePiece[] pieces)
    {
        if (pieces.Length == 0)
            yield break;

        yield return Count(pieces.Length, "piece");
        if (type == StructureType.EndCity)
        {
            yield return pieces.Any(piece => piece.Type == EndCityPiece.Ship) ? "Has an end ship" : "No end ship";
        }
        else if (type == StructureType.Fortress)
        {
            yield return Count(pieces.Count(piece => piece.Type == FortressPiece.BlazeSpawner), "blaze spawner");
            yield return Count(pieces.Count(piece => piece.Type == FortressPiece.NetherWartRoom), "nether wart room");
        }
    }

    private static string Count(int count, string noun)
        => string.Create(CultureInfo.InvariantCulture, $"{count} {noun}{(count == 1 ? string.Empty : "s")}");
}

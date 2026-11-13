using System.Collections.Concurrent;

using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class StructureFeatureCatalog : IStructureFeatureCatalog
{
    private const int PlainsBiome = 1;
    private const int DesertBiome = 2;
    private const int TaigaBiome = 5;
    private const int SnowyBiome = 12;
    private const int SavannaBiome = 35;
    private const int BastionTypes = 4;

    private static readonly Definition _villageType = new("type", "Type",
        [("plains", "Plains"), ("desert", "Desert"), ("savanna", "Savanna"), ("taiga", "Taiga"), ("snowy", "Snowy")],
        static details => details.Variant?.Biome switch
        {
            PlainsBiome => 0,
            DesertBiome => 1,
            SavannaBiome => 2,
            TaigaBiome => 3,
            SnowyBiome => 4,
            _ => null,
        });

    private static readonly Definition _villagers = Choice("villagers", "Villagers", ("living", "Living"), ("abandoned", "Abandoned"), static variant => variant.IsAbandoned);

    private static readonly Definition _bastionType = new("type", "Type",
        [("housingUnits", "Housing units"), ("hoglinStables", "Hoglin stables"), ("treasureRoom", "Treasure room"), ("bridge", "Bridge")],
        static details => details.Variant?.Start is int start and >= 0 and < BastionTypes ? start : null);

    private static readonly Definition _portalSize = Choice("size", "Size", ("normal", "Normal"), ("giant", "Giant"), static variant => variant.IsGiant);
    private static readonly Definition _portalPlacement = Choice("placement", "Placement", ("surface", "On the surface"), ("buried", "Buried"), static variant => variant.IsUnderground);
    private static readonly Definition _shell = Choice("shell", "Shell", ("cracked", "Cracked open"), ("closed", "Closed"), static variant => !variant.IsCracked);
    private static readonly Definition _basement = Presence("basement", "Basement", static details => details.Variant?.HasBasement);
    private static readonly Definition _endShip = Presence("endShip", "End ship", static details => HasPiece(details, EndCityPiece.Ship));
    private static readonly Definition _netherWartRoom = Presence("netherWartRoom", "Nether wart room", static details => HasPiece(details, FortressPiece.NetherWartRoom));
    private static readonly Definition _blazeSpawner = Presence("blazeSpawner", "Blaze spawner", static details => HasPiece(details, FortressPiece.BlazeSpawner));

    private readonly ConcurrentDictionary<(StructureType Type, McVersion Version), Layout> _layouts = new();

    public IReadOnlyList<StructureFeature> GetFeatures(StructureType type, McVersion version) => GetLayout(type, version).Features;

    public ulong GetOptions(StructureDetails details, McVersion version)
    {
        ArgumentNullException.ThrowIfNull(details);

        var layout = GetLayout(details.Type, version);
        ulong options = 0;
        for (var index = 0; index < layout.Definitions.Length; index++)
        {
            if (layout.Definitions[index].OptionOf(details) is { } option)
                options |= layout.Features[index].Options[option].Bit;
        }

        return options;
    }

    private Layout GetLayout(StructureType type, McVersion version)
        => _layouts.GetOrAdd((type, version), static key => Layout.Create(GetDefinitions(key.Type, key.Version)));

    /// <summary>The features the generator can tell apart: village types from 1.14, abandoned villages from 1.10.</summary>
    private static Definition[] GetDefinitions(StructureType type, McVersion version) => type switch
    {
        StructureType.Village when version >= McVersion.V1_14_4 => [_villageType, _villagers],
        StructureType.Village when version >= McVersion.V1_10_2 => [_villagers],
        StructureType.Bastion => [_bastionType],
        StructureType.RuinedPortal => [_portalSize, _portalPlacement],
        StructureType.RuinedPortalNether => [_portalSize],
        StructureType.Igloo => [_basement],
        StructureType.Geode => [_shell],
        StructureType.EndCity => [_endShip],
        StructureType.Fortress => [_netherWartRoom, _blazeSpawner],
        _ => [],
    };

    private static Definition Choice(string key, string name, (string Key, string Name) whenFalse, (string Key, string Name) whenTrue, Func<StructureVariant, bool> isTrue)
        => new(key, name, [whenFalse, whenTrue], details => details.Variant is { } variant ? (isTrue(variant) ? 1 : 0) : null);

    private static Definition Presence(string key, string name, Func<StructureDetails, bool?> has)
        => new(key, name, [("with", "With"), ("without", "Without")], details => has(details) switch
        {
            true => 0,
            false => 1,
            null => null,
        });

    private static bool HasPiece(StructureDetails details, int pieceType) => details.Pieces.Any(piece => piece.Type == pieceType);

    /// <summary>A feature with the way to tell which of its options a described structure has, or <see langword="null"/> when the details do not say.</summary>
    private sealed record Definition(string Key, string Name, (string Key, string Name)[] Options, Func<StructureDetails, int?> OptionOf);

    /// <summary>The features of one type and version, whose options are numbered one after the other across all of them.</summary>
    private sealed class Layout(Definition[] definitions, StructureFeature[] features)
    {
        public Definition[] Definitions { get; } = definitions;
        public StructureFeature[] Features { get; } = features;

        public static Layout Create(Definition[] definitions)
        {
            var bit = 0;
            var features = new StructureFeature[definitions.Length];
            for (var index = 0; index < definitions.Length; index++)
            {
                var definition = definitions[index];
                var options = new StructureFeatureOption[definition.Options.Length];
                for (var option = 0; option < options.Length; option++)
                    options[option] = new StructureFeatureOption { Key = definition.Options[option].Key, Name = definition.Options[option].Name, Bit = 1UL << bit++ };

                features[index] = new StructureFeature { Key = definition.Key, Name = definition.Name, Options = options };
            }

            return new Layout(definitions, features);
        }
    }
}

using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.SelfTestServ;

/// <summary>
/// Regression values for structures, strongholds, the spawn point and slime chunks of one seed, recorded from the
/// Windows x64 reference build. A build for another platform or compiler must reproduce them exactly.
/// </summary>
internal static class ReferenceStructureHash
{
    public const long Seed = 262;
    public const int SlimeChunkOrigin = -64;
    public const int SlimeChunkSide = 128;
    public const uint SlimeChunkHash = 0x84463004;

    /// <summary>The hash of nothing, from which combined hashes start.</summary>
    public const uint EmptyHash = FnvOffsetBasis;

    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;
    private const int WideSide = 8192;
    private const int DenseSide = 2048;

    private static readonly BlockArea _wideArea = new(-WideSide / 2, -WideSide / 2, WideSide, WideSide);
    private static readonly BlockArea _denseArea = new(-DenseSide / 2, -DenseSide / 2, DenseSide, DenseSide);
    private static readonly BlockArea _outerEndArea = new(2048, -DenseSide / 2, DenseSide, DenseSide);
    private static readonly BlockArea _detailArea = new(-DenseSide, -DenseSide, 2 * DenseSide, 2 * DenseSide);

    public static IReadOnlyList<StructureCase> StructureCases { get; } =
    [
        new("1.21 Overworld", McVersion.V1_21_3, Dimension.Overworld, _wideArea, 0x6e98f5d9,
            StructureType.Village, StructureType.DesertPyramid, StructureType.JungleTemple, StructureType.SwampHut, StructureType.Igloo,
            StructureType.Monument, StructureType.Mansion, StructureType.Outpost, StructureType.Shipwreck, StructureType.OceanRuin,
            StructureType.RuinedPortal, StructureType.AncientCity, StructureType.TrailRuins, StructureType.TrialChambers),
        new("1.21 Overworld features", McVersion.V1_21_3, Dimension.Overworld, _denseArea, 0x9b052b2f,
            StructureType.Treasure, StructureType.Mineshaft, StructureType.DesertWell, StructureType.Geode),
        new("1.21 Nether", McVersion.V1_21_3, Dimension.Nether, _wideArea, 0x6b2f4657,
            StructureType.Fortress, StructureType.Bastion, StructureType.RuinedPortalNether),
        new("1.21 End", McVersion.V1_21_3, Dimension.End, _wideArea, 0xc26d0f8d, StructureType.EndCity),
        new("1.21 End gateways", McVersion.V1_21_3, Dimension.End, _outerEndArea, 0x8d017492, StructureType.EndGateway),
        new("1.16 Overworld", McVersion.V1_16_5, Dimension.Overworld, _wideArea, 0xe966815b,
            StructureType.Village, StructureType.SwampHut, StructureType.Monument, StructureType.Mansion, StructureType.Outpost, StructureType.Shipwreck),
        new("1.16 Nether", McVersion.V1_16_5, Dimension.Nether, _wideArea, 0x098d2173, StructureType.Fortress, StructureType.Bastion),
        new("1.12 Overworld", McVersion.V1_12_2, Dimension.Overworld, _wideArea, 0x35be51d1,
            StructureType.Village, StructureType.DesertPyramid, StructureType.Igloo, StructureType.Monument, StructureType.Mansion),
        new("1.12 End", McVersion.V1_12_2, Dimension.End, _wideArea, 0xeda615cb, StructureType.EndCity),
    ];

    /// <summary>Variants and pieces of every structure of the listed types inside the area.</summary>
    public static IReadOnlyList<StructureCase> DetailCases { get; } =
    [
        new("1.21 Overworld", McVersion.V1_21_3, Dimension.Overworld, _wideArea, 0xf46fab74,
            StructureType.Village, StructureType.RuinedPortal, StructureType.Igloo, StructureType.AncientCity, StructureType.TrialChambers, StructureType.DesertPyramid),
        new("1.21 Nether", McVersion.V1_21_3, Dimension.Nether, _detailArea, 0xbbb192c2, StructureType.Fortress, StructureType.Bastion),
        new("1.21 End", McVersion.V1_21_3, Dimension.End, _wideArea, 0x88241018, StructureType.EndCity),
        new("1.12 Nether", McVersion.V1_12_2, Dimension.Nether, _detailArea, 0xf0d409ac, StructureType.Fortress),
    ];

    public static IReadOnlyList<(McVersion Version, uint Hash)> GatewayHashes { get; } =
    [
        (McVersion.V1_21_3, 0x4d202886),
        (McVersion.V1_16_5, 0x08eee367),
    ];

    public static IReadOnlyList<(McVersion Version, uint Hash)> StrongholdHashes { get; } =
    [
        (McVersion.V1_21_3, 0x2954cdf6),
        (McVersion.V1_16_5, 0xe3256cce),
        (McVersion.V1_12_2, 0x7997c40e),
        (McVersion.V1_8_9, 0x3763cd71),
    ];

    public static IReadOnlyList<(McVersion Version, BlockPos Spawn)> Spawns { get; } =
    [
        (McVersion.V1_21_3, new BlockPos(-544, -432)),
        (McVersion.V1_17_1, new BlockPos(160, -240)),
        (McVersion.V1_12_2, new BlockPos(-12, -68)),
    ];

    /// <summary>FNV-1a over the coordinates, continuing from <paramref name="hash"/>.</summary>
    public static uint Hash(ReadOnlySpan<BlockPos> positions, uint hash = FnvOffsetBasis)
    {
        foreach (var position in positions)
        {
            hash = Hash(position.X, hash);
            hash = Hash(position.Z, hash);
        }

        return hash;
    }

    public static uint Hash(StructureDetails details, uint hash)
    {
        hash = Hash([details.Position], hash);
        if (details.Variant is { } variant)
        {
            int[] values =
            [
                Flag(variant.IsAbandoned), Flag(variant.IsGiant), Flag(variant.IsUnderground), Flag(variant.HasAirPocket), Flag(variant.HasBasement),
                Flag(variant.IsCracked), Flag(variant.IsMirrored), variant.Size, variant.Start, variant.Biome, variant.Rotation, variant.Height ?? int.MinValue,
            ];
            hash = Hash(values, hash);
            if (variant.Box is { } box)
                hash = Hash(box, hash);
        }

        foreach (var piece in details.Pieces)
        {
            hash = Hash([piece.Type, piece.Rotation, piece.Depth], hash);
            hash = Hash(piece.Box, hash);
        }

        return hash;
    }

    public static uint Hash(ReadOnlySpan<EndGateway> gateways)
    {
        var hash = FnvOffsetBasis;
        foreach (var gateway in gateways)
            hash = Hash([gateway.Position, gateway.Destination], hash);
        return hash;
    }

    public static uint Hash(ReadOnlySpan<byte> bytes)
    {
        var hash = FnvOffsetBasis;
        foreach (var value in bytes)
            hash = (hash ^ value) * FnvPrime;
        return hash;
    }

    private static uint Hash(BlockBox box, uint hash)
        => Hash([box.MinX, box.MinY, box.MinZ, box.MaxX, box.MaxY, box.MaxZ], hash);

    private static uint Hash(ReadOnlySpan<int> values, uint hash)
    {
        foreach (var value in values)
            hash = Hash(value, hash);
        return hash;
    }

    private static int Flag(bool value) => value ? 1 : 0;

    private static uint Hash(int value, uint hash)
    {
        for (var shift = 0; shift < 32; shift += 8)
            hash = (hash ^ (byte)(value >> shift)) * FnvPrime;
        return hash;
    }

    public sealed class StructureCase(string name, McVersion version, Dimension dimension, BlockArea area, uint hash, params StructureType[] types)
    {
        public string Name { get; } = name;
        public McVersion Version { get; } = version;
        public Dimension Dimension { get; } = dimension;
        public BlockArea Area { get; } = area;
        public uint Hash { get; } = hash;
        public IReadOnlyList<StructureType> Types { get; } = types;
    }
}

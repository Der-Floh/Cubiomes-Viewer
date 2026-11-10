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

    private const uint FnvOffsetBasis = 2166136261;
    private const uint FnvPrime = 16777619;
    private const int WideSide = 8192;
    private const int DenseSide = 2048;

    private static readonly BlockArea _wideArea = new(-WideSide / 2, -WideSide / 2, WideSide, WideSide);
    private static readonly BlockArea _denseArea = new(-DenseSide / 2, -DenseSide / 2, DenseSide, DenseSide);
    private static readonly BlockArea _outerEndArea = new(2048, -DenseSide / 2, DenseSide, DenseSide);

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

    public static uint Hash(ReadOnlySpan<byte> bytes)
    {
        var hash = FnvOffsetBasis;
        foreach (var value in bytes)
            hash = (hash ^ value) * FnvPrime;
        return hash;
    }

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

using System.Buffers;

using Cubiomes_Viewer.Engine.Data.Loot;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;

namespace Cubiomes_Viewer.Engine.Service.WorldGenServ;

public sealed class CubiomesWorldGenerator : IWorldGenerator
{
    private const int MaxStrongholds = 128;
    private const int MaxPieces = 16_384;
    private const int EndGatewayCount = 20;
    private const int ValuesPerGateway = 4;
    private const int UnknownHeight = 320;
    private const int LootContainerCapacity = 64;
    private const int MaxLootContainerCapacity = 4_096;
    private const int StacksPerContainer = 27;
    private const int Supported = 1;

    public McVersion Version { get; }
    public GeneratorFlags Flags { get; }
    public StructureRules Rules { get; }

    public bool HasExactTerrain
        => _dimension is { } dimension && CubiomesNativeException.ThrowIfError(CubiomesNative.TerrainSupported((int)Version, (int)dimension), nameof(HasExactTerrain)) == Supported;

    private readonly WorldHandle _world;
    private Dimension? _dimension;

    public CubiomesWorldGenerator(McVersion version, GeneratorFlags flags, StructureRules? rules = null)
    {
        var world = CubiomesNative.WorldCreate((int)version, (uint)flags);
        if (world == 0)
            throw new ArgumentException($"The native generator does not support Minecraft version '{version}' with flags '{flags}'.", nameof(version));

        _world = new WorldHandle(world);
        Version = version;
        Flags = flags;
        Rules = rules ?? StructureRules.Default;
        ApplyRules();
    }

    private unsafe void ApplyRules()
    {
        var native = NativeRules.From(Rules);
        CubiomesNativeException.ThrowIfError(CubiomesNative.WorldSetRules(World, &native), nameof(ApplyRules));
    }

    public void Apply(Dimension dimension, long seed)
    {
        CubiomesNativeException.ThrowIfError(CubiomesNative.WorldApply(World, (int)dimension, unchecked((ulong)seed)), nameof(Apply));
        _dimension = dimension;
    }

    public unsafe int GetBiomeAt(int scale, int x, int y, int z)
    {
        int biome;
        CubiomesNativeException.ThrowIfError(CubiomesNative.BiomeAt(World, scale, x, y, z, &biome), nameof(GetBiomeAt));
        return biome;
    }

    public unsafe void GenerateBiomes(BiomeArea area, Span<byte> destination)
    {
        ThrowIfTooSmall(area, destination.Length);

        fixed (byte* buffer = destination)
        {
            var result = CubiomesNative.GenerateBiomes(World, area.Scale, area.X, area.Z, area.SizeX, area.SizeZ, area.Y, buffer, destination.Length);
            CubiomesNativeException.ThrowIfError(result, nameof(GenerateBiomes));
        }
    }

    public unsafe void GenerateClimate(ClimateParameter parameter, int octaves, BiomeArea area, Span<short> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(octaves);
        ThrowIfTooSmall(area, destination.Length);

        fixed (short* buffer = destination)
        {
            var result = CubiomesNative.GenerateClimate(World, (int)parameter, octaves, area.Scale, area.X, area.Z, area.SizeX, area.SizeZ, area.Y, buffer, destination.Length);
            CubiomesNativeException.ThrowIfError(result, nameof(GenerateClimate));
        }
    }

    public unsafe void GenerateLayer(GeneratorLayer layer, BiomeArea area, Span<byte> destination)
    {
        ThrowIfTooSmall(area, destination.Length);

        fixed (byte* buffer = destination)
        {
            var result = CubiomesNative.GenerateLayer(World, (int)layer, area.Scale, area.X, area.Z, area.SizeX, area.SizeZ, buffer, destination.Length);
            CubiomesNativeException.ThrowIfError(result, nameof(GenerateLayer));
        }
    }

    public unsafe void GenerateHeights(BiomeArea area, Span<short> destination)
    {
        ThrowIfTooSmall(area, destination.Length);

        fixed (short* buffer = destination)
        {
            var result = CubiomesNative.GenerateHeights(World, area.Scale, area.X, area.Z, area.SizeX, area.SizeZ, buffer, destination.Length);
            CubiomesNativeException.ThrowIfError(result, nameof(GenerateHeights));
        }
    }

    public unsafe void GenerateSurface(BlockArea area, Span<short> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(area.SizeX);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(area.SizeZ);
        if (destination.Length < area.SizeX * area.SizeZ)
            throw new ArgumentException($"The destination holds {destination.Length} columns but the area has {area.SizeX * area.SizeZ}.", nameof(destination));

        fixed (short* buffer = destination)
        {
            var result = CubiomesNative.GenerateSurface(World, area.X, area.Z, area.SizeX, area.SizeZ, buffer, destination.Length);
            CubiomesNativeException.ThrowIfError(result, nameof(GenerateSurface));
        }
    }

    public unsafe int? FindStructureFloor(StructureType type, BlockPos position)
    {
        int y;
        var found = CubiomesNative.StructureFloor(World, (int)type, position.X, position.Z, &y);
        return CubiomesNativeException.ThrowIfError(found, nameof(FindStructureFloor)) > 0 ? y : null;
    }

    public StructureLoot? FindLoot(StructureType type, BlockPos position)
    {
        var capacity = LootContainerCapacity;
        StructureLoot? loot;
        while (!TryFindLoot(type, position, capacity, out loot))
            capacity *= 2;

        return loot;
    }

    public unsafe LootCount? CountLoot(StructureType type, BlockPos position, LootQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);

        var native = NativeLootConverter.ToNativeQuery(query);
        int most, total;
        var containers = CubiomesNative.StructureLootCount(World, (int)type, position.X, position.Z, &native, &most, &total);
        if (containers == (int)NativeStatus.Unsupported)
            return null;

        return new LootCount(CubiomesNativeException.ThrowIfError(containers, nameof(CountLoot)), most, total);
    }

    public bool CanGenerateStructure(StructureType type, BlockPos position)
    {
        var result = CubiomesNative.StructureViable(World, (int)type, position.X, position.Z);
        return CubiomesNativeException.ThrowIfError(result, nameof(CanGenerateStructure)) > 0;
    }

    public unsafe BlockPos[] FindStructures(StructureType type, BlockArea area)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(area.SizeX);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(area.SizeZ);

        var capacity = GetMaxStructureCount(type, area);
        if (capacity == 0)
            return [];

        var buffer = ArrayPool<BlockPos>.Shared.Rent(capacity);
        try
        {
            return buffer.AsSpan(0, FindStructures(type, area, buffer)).ToArray();
        }
        finally
        {
            ArrayPool<BlockPos>.Shared.Return(buffer);
        }
    }

    public unsafe int FindStructures(StructureType type, BlockArea area, Span<BlockPos> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(area.SizeX);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(area.SizeZ);

        fixed (BlockPos* positions = destination)
        {
            var count = CubiomesNative.StructuresInArea(World, (int)type, area.X, area.Z, area.Right, area.Bottom, (int*)positions, destination.Length);
            return CubiomesNativeException.ThrowIfError(count, nameof(FindStructures));
        }
    }

    public StructureDetails DescribeStructure(StructureType type, BlockPos position)
        => new()
        {
            Type = type,
            Position = position,
            Variant = FindVariant(type, position),
            Pieces = FindPieces(type, position),
        };

    public unsafe EndGateway[] FindEndGateways()
    {
        var positions = stackalloc int[EndGatewayCount * ValuesPerGateway];
        var count = CubiomesNative.EndGateways(World, positions, EndGatewayCount);
        CubiomesNativeException.ThrowIfError(count, nameof(FindEndGateways));

        var gateways = new EndGateway[count];
        for (var index = 0; index < count; index++)
        {
            var values = positions + index * ValuesPerGateway;
            gateways[index] = new EndGateway(new BlockPos(values[0], values[1]), new BlockPos(values[2], values[3]));
        }

        return gateways;
    }

    public unsafe BlockPos? FindSpawn()
    {
        int x, z;
        var found = CubiomesNativeException.ThrowIfError(CubiomesNative.Spawn(World, &x, &z), nameof(FindSpawn));
        return found > 0 ? new BlockPos(x, z) : null;
    }

    public BlockPos[] FindStrongholds()
    {
        Span<BlockPos> buffer = stackalloc BlockPos[MaxStrongholds];
        return buffer[..FindStrongholds(buffer)].ToArray();
    }

    public unsafe int FindStrongholds(Span<BlockPos> destination)
    {
        fixed (BlockPos* positions = destination)
        {
            var count = CubiomesNative.Strongholds(World, (int*)positions, destination.Length);
            return CubiomesNativeException.ThrowIfError(count, nameof(FindStrongholds));
        }
    }

    public void Dispose() => _world.Dispose();

    private static void ThrowIfTooSmall(BiomeArea area, int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(area.SizeX);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(area.SizeZ);
        if (capacity < area.SampleCount)
            throw new ArgumentException($"The destination holds {capacity} samples but the area needs {area.SampleCount}.", nameof(area));
    }

    private unsafe StructureVariant? FindVariant(StructureType type, BlockPos position)
    {
        NativeVariant variant;
        var found = CubiomesNative.StructureVariant(World, (int)type, position.X, position.Z, &variant);
        if (CubiomesNativeException.ThrowIfError(found, nameof(DescribeStructure)) == 0)
            return null;

        return new StructureVariant
        {
            IsAbandoned = variant.Abandoned != 0,
            IsGiant = variant.Giant != 0,
            IsUnderground = variant.Underground != 0,
            HasAirPocket = variant.AirPocket != 0,
            HasBasement = variant.Basement != 0,
            IsCracked = variant.Cracked != 0,
            IsMirrored = variant.Mirror != 0,
            Size = variant.Size,
            Start = variant.Start,
            Biome = variant.Biome,
            Rotation = variant.Rotation,
            Height = variant.Y == UnknownHeight ? null : variant.Y,
            Box = GetFirstPieceBox(position, variant),
            HasDriedGhast = variant.DriedGhast != 0,
            PortalEyes = variant.PortalEyes,
        };
    }

    private static BlockBox? GetFirstPieceBox(BlockPos position, NativeVariant variant)
    {
        if (variant.SizeX <= 0 || variant.SizeZ <= 0)
            return null;

        var minX = position.X + variant.X;
        var minZ = position.Z + variant.Z;
        var minY = variant.Y == UnknownHeight ? 0 : variant.Y;
        return new BlockBox(minX, minY, minZ, minX + variant.SizeX - 1, minY + Math.Max(variant.SizeY, 1) - 1, minZ + variant.SizeZ - 1);
    }

    private unsafe StructurePiece[] FindPieces(StructureType type, BlockPos position)
    {
        var buffer = ArrayPool<NativePiece>.Shared.Rent(MaxPieces);
        try
        {
            int count;
            fixed (NativePiece* pieces = buffer)
                count = CubiomesNative.StructurePieces(World, (int)type, position.X, position.Z, pieces, buffer.Length);

            CubiomesNativeException.ThrowIfError(count, nameof(DescribeStructure));
            return Array.ConvertAll(buffer.AsSpan(0, count).ToArray(), ToPiece);
        }
        finally
        {
            ArrayPool<NativePiece>.Shared.Return(buffer);
        }
    }

    /// <returns><see langword="false"/> when the structure has more containers than <paramref name="capacity"/>.</returns>
    private unsafe bool TryFindLoot(StructureType type, BlockPos position, int capacity, out StructureLoot? loot)
    {
        var containers = ArrayPool<NativeLootContainer>.Shared.Rent(capacity);
        var stacks = ArrayPool<NativeLootStack>.Shared.Rent(capacity * StacksPerContainer);
        try
        {
            int count;
            fixed (NativeLootContainer* containerBuffer = containers)
            fixed (NativeLootStack* stackBuffer = stacks)
                count = CubiomesNative.StructureLoot(World, (int)type, position.X, position.Z, containerBuffer, containers.Length, stackBuffer, stacks.Length);

            loot = null;
            if (count == (int)NativeStatus.InsufficientCapacity && capacity < MaxLootContainerCapacity)
                return false;
            if (count == (int)NativeStatus.Unsupported)
                return true;

            count = CubiomesNativeException.ThrowIfError(count, nameof(FindLoot));
            loot = StructureLoot.Of(NativeLootConverter.ToContainers(containers.AsSpan(0, count), stacks, Version));
            return true;
        }
        finally
        {
            ArrayPool<NativeLootStack>.Shared.Return(stacks);
            ArrayPool<NativeLootContainer>.Shared.Return(containers);
        }
    }

    private static StructurePiece ToPiece(NativePiece piece)
        => new(piece.Type, piece.Rotation, piece.Depth, new BlockBox(piece.X0, piece.Y0, piece.Z0, piece.X1, piece.Y1, piece.Z1));

    /// <summary>Every region that touches the area holds at most one structure.</summary>
    private unsafe int GetMaxStructureCount(StructureType type, BlockArea area)
    {
        int dimension, regionBlocks;
        var exists = CubiomesNative.StructureConfig((int)type, (int)Version, &dimension, &regionBlocks);
        if (CubiomesNativeException.ThrowIfError(exists, nameof(FindStructures)) == 0)
            return 0;

        return checked((int)new StructurePlacement((Dimension)dimension, regionBlocks).CountRegions(area));
    }

    private nint World
    {
        get
        {
            ObjectDisposedException.ThrowIf(_world.IsClosed, this);
            return _world.DangerousGetHandle();
        }
    }
}

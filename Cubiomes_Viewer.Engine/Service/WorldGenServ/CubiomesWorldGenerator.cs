using System.Buffers;

using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;

namespace Cubiomes_Viewer.Engine.Service.WorldGenServ;

public sealed class CubiomesWorldGenerator : IWorldGenerator
{
    private const int MaxStrongholds = 128;

    public McVersion Version { get; }
    public GeneratorFlags Flags { get; }

    private readonly WorldHandle _world;

    public CubiomesWorldGenerator(McVersion version, GeneratorFlags flags)
    {
        var world = CubiomesNative.WorldCreate((int)version, (uint)flags);
        if (world == 0)
            throw new ArgumentException($"The native generator does not support Minecraft version '{version}' with flags '{flags}'.", nameof(version));

        _world = new WorldHandle(world);
        Version = version;
        Flags = flags;
    }

    public void Apply(Dimension dimension, long seed)
        => CubiomesNativeException.ThrowIfError(CubiomesNative.WorldApply(World, (int)dimension, unchecked((ulong)seed)), nameof(Apply));

    public unsafe int GetBiomeAt(int scale, int x, int y, int z)
    {
        int biome;
        CubiomesNativeException.ThrowIfError(CubiomesNative.BiomeAt(World, scale, x, y, z, &biome), nameof(GetBiomeAt));
        return biome;
    }

    public unsafe void GenerateBiomes(BiomeArea area, Span<byte> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(area.SizeX);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(area.SizeZ);
        if (destination.Length < area.SampleCount)
            throw new ArgumentException($"The destination holds {destination.Length} samples but the area needs {area.SampleCount}.", nameof(destination));

        fixed (byte* buffer = destination)
        {
            var result = CubiomesNative.GenerateBiomes(World, area.Scale, area.X, area.Z, area.SizeX, area.SizeZ, area.Y, buffer, destination.Length);
            CubiomesNativeException.ThrowIfError(result, nameof(GenerateBiomes));
        }
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
            int count;
            fixed (BlockPos* positions = buffer)
                count = CubiomesNative.StructuresInArea(World, (int)type, area.X, area.Z, area.Right, area.Bottom, (int*)positions, buffer.Length);

            CubiomesNativeException.ThrowIfError(count, nameof(FindStructures));
            return buffer.AsSpan(0, count).ToArray();
        }
        finally
        {
            ArrayPool<BlockPos>.Shared.Return(buffer);
        }
    }

    public unsafe BlockPos? FindSpawn()
    {
        int x, z;
        var found = CubiomesNativeException.ThrowIfError(CubiomesNative.Spawn(World, &x, &z), nameof(FindSpawn));
        return found > 0 ? new BlockPos(x, z) : null;
    }

    public unsafe BlockPos[] FindStrongholds()
    {
        var buffer = stackalloc BlockPos[MaxStrongholds];
        var count = CubiomesNative.Strongholds(World, (int*)buffer, MaxStrongholds);
        CubiomesNativeException.ThrowIfError(count, nameof(FindStrongholds));
        return new ReadOnlySpan<BlockPos>(buffer, count).ToArray();
    }

    public void Dispose() => _world.Dispose();

    /// <summary>Every region that touches the area holds at most one structure.</summary>
    private unsafe int GetMaxStructureCount(StructureType type, BlockArea area)
    {
        int dimension, regionBlocks;
        var exists = CubiomesNative.StructureConfig((int)type, (int)Version, &dimension, &regionBlocks);
        if (CubiomesNativeException.ThrowIfError(exists, nameof(FindStructures)) == 0)
            return 0;

        var regionsX = Math.Floor((area.Right - 1) / (double)regionBlocks) - Math.Floor(area.X / (double)regionBlocks) + 1;
        var regionsZ = Math.Floor((area.Bottom - 1) / (double)regionBlocks) - Math.Floor(area.Z / (double)regionBlocks) + 1;
        return checked((int)(regionsX * regionsZ));
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

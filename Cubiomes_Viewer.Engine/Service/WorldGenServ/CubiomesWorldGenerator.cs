using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;

namespace Cubiomes_Viewer.Engine.Service.WorldGenServ;

public sealed class CubiomesWorldGenerator : IWorldGenerator
{
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

    public void Dispose() => _world.Dispose();

    private nint World
    {
        get
        {
            ObjectDisposedException.ThrowIf(_world.IsClosed, this);
            return _world.DangerousGetHandle();
        }
    }
}

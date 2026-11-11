using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class CubiomesStructureFinder : IStructureFinder
{
    public unsafe bool TryGetPlacement(StructureType type, McVersion version, out StructurePlacement placement)
    {
        int dimension, regionBlocks;
        var result = CubiomesNative.StructureConfig((int)type, (int)version, &dimension, &regionBlocks);
        CubiomesNativeException.ThrowIfError(result, nameof(TryGetPlacement));

        placement = new StructurePlacement((Dimension)dimension, regionBlocks);
        return result > 0;
    }

    public unsafe bool TryGetRegionPosition(StructureType type, McVersion version, long seed, int regionX, int regionZ, out BlockPos position)
    {
        int x, z;
        var result = CubiomesNative.StructurePosition((int)type, (int)version, unchecked((ulong)seed), regionX, regionZ, &x, &z);
        CubiomesNativeException.ThrowIfError(result, nameof(TryGetRegionPosition));

        position = new BlockPos(x, z);
        return result > 0;
    }

    public unsafe int FindAttempts(StructureType type, McVersion version, long seed, BlockArea area, Span<BlockPos> destination)
    {
        fixed (BlockPos* positions = destination)
        {
            var count = CubiomesNative.StructureAttemptsInArea(
                (int)type, (int)version, unchecked((ulong)seed), area.X, area.Z, area.Right, area.Bottom, (int*)positions, destination.Length);
            return CubiomesNativeException.ThrowIfError(count, nameof(FindAttempts));
        }
    }

    public bool TryGetViableBiomes(StructureType type, McVersion version, [NotNullWhen(true)] out IReadOnlySet<int>? biomes)
    {
        List<int> viable = [];
        for (var biome = 0; biome < Constants.NoBiomeTileValue; biome++)
        {
            var result = CubiomesNative.StructureBiomeViable((int)type, (int)version, biome);
            if (result == (int)NativeStatus.Unsupported)
            {
                biomes = null;
                return false;
            }

            if (CubiomesNativeException.ThrowIfError(result, nameof(TryGetViableBiomes)) > 0)
                viable.Add(biome);
        }

        biomes = viable.ToFrozenSet();
        return true;
    }
}

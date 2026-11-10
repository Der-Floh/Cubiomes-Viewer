using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class CubiomesStructureFinder : IStructureFinder
{
    public unsafe bool TryGetRegionPosition(StructureType type, McVersion version, long seed, int regionX, int regionZ, out BlockPos position)
    {
        int x, z;
        var result = CubiomesNative.StructurePosition((int)type, (int)version, unchecked((ulong)seed), regionX, regionZ, &x, &z);
        CubiomesNativeException.ThrowIfError(result, nameof(TryGetRegionPosition));

        position = new BlockPos(x, z);
        return result > 0;
    }
}

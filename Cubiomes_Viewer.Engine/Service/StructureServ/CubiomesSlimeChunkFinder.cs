using Cubiomes_Viewer.Engine.Interop;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class CubiomesSlimeChunkFinder : ISlimeChunkFinder
{
    public unsafe void FindSlimeChunks(long seed, int chunkX, int chunkZ, int sizeX, int sizeZ, Span<byte> destination)
    {
        fixed (byte* buffer = destination)
        {
            var result = CubiomesNative.SlimeChunks(unchecked((ulong)seed), chunkX, chunkZ, sizeX, sizeZ, buffer, destination.Length);
            CubiomesNativeException.ThrowIfError(result, nameof(FindSlimeChunks));
        }
    }
}

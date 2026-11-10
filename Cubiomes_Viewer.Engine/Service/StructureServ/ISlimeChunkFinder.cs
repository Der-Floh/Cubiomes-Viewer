namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public interface ISlimeChunkFinder
{
    /// <summary>
    /// Marks the slime chunks of a rectangle of chunks: <paramref name="destination"/> receives one byte per chunk,
    /// row by row (index <c>z * sizeX + x</c>), <c>1</c> for a slime chunk and <c>0</c> otherwise.
    /// </summary>
    void FindSlimeChunks(long seed, int chunkX, int chunkZ, int sizeX, int sizeZ, Span<byte> destination);
}

namespace Cubiomes_Viewer.Engine.Data.Structures;

public sealed class SlimeAreaData
{
    private const byte SlimeChunk = 1;

    public required SlimeAreaRequest Request { get; init; }

    /// <summary>One byte per chunk, row by row; <c>1</c> marks a slime chunk.</summary>
    public required byte[] Chunks { get; init; }

    /// <param name="offsetX">The chunk column inside the area, starting at zero.</param>
    /// <param name="offsetZ">The chunk row inside the area, starting at zero.</param>
    public bool IsSlimeChunk(int offsetX, int offsetZ) => Chunks[offsetZ * Request.SizeChunks + offsetX] == SlimeChunk;
}

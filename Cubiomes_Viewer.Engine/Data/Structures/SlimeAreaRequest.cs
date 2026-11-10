namespace Cubiomes_Viewer.Engine.Data.Structures;

/// <summary>A square of <see cref="SizeChunks"/> × <see cref="SizeChunks"/> chunks whose slime chunks are wanted; used as the cache key.</summary>
public readonly record struct SlimeAreaRequest(long Seed, int ChunkX, int ChunkZ, int SizeChunks);

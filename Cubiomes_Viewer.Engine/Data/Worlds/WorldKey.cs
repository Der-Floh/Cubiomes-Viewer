namespace Cubiomes_Viewer.Engine.Data.Worlds;

public readonly record struct WorldKey(long Seed, McVersion Version, Dimension Dimension, GeneratorFlags Flags);

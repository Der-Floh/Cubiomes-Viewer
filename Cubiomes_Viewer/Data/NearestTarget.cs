using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Data;

/// <summary>What Find nearest looks for, named as the menu lists it.</summary>
public abstract record NearestTarget(string Name);

public sealed record StructureTarget(string Name, StructureType Type) : NearestTarget(Name);

public sealed record StrongholdTarget(string Name) : NearestTarget(Name);

public sealed record BiomeTarget(string Name, int Biome) : NearestTarget(Name);

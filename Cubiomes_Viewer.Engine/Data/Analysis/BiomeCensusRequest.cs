using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Analysis;

/// <summary>
/// The biomes of one world in an area around a reference point, sampled every <see cref="Scale"/> blocks at height
/// <see cref="Y"/>. The samples are those the biome condition takes for the same area, and the nearest of each biome
/// is measured from the reference point.
/// </summary>
public sealed record BiomeCensusRequest(WorldKey World, SearchArea Area, BlockPos Reference, int Scale, int Y);

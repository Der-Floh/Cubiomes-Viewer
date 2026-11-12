using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Data.Compute;

/// <summary>Counts the biomes of <paramref name="RowCount"/> rows of the sample grid of an area, from row <paramref name="FirstRow"/> on.</summary>
public sealed record BiomeCensusJob(BiomeCensusRequest Request, int FirstRow, int RowCount) : ComputeJob<BiomeCensus>;

/// <summary>Lists the structures of one type in an area.</summary>
public sealed record StructureListJob(StructureListRequest Request) : ComputeJob<StructureListing>;

/// <summary>Lists the quads of one structure type in an area.</summary>
public sealed record QuadListJob(QuadListRequest Request) : ComputeJob<QuadListing>;

/// <summary>Finds where the conditions of a definition hold for one seed.</summary>
public sealed record ConditionLocationsJob(SearchDefinition Definition, long Seed) : ComputeJob<ConditionLocations>;

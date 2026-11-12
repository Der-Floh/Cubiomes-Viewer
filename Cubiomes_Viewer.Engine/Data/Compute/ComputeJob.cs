using System.Text.Json.Serialization;

namespace Cubiomes_Viewer.Engine.Data.Compute;

/// <summary>
/// A small, self-contained unit of work. Jobs carry everything a worker needs, so any worker
/// (a thread, or a Web Worker in the browser) can execute any job. Every job type is listed here
/// so it can be sent to a worker in another runtime.
/// </summary>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "$job")]
[JsonDerivedType(typeof(TileJob), "tile")]
[JsonDerivedType(typeof(StructureAreaJob), "structureArea")]
[JsonDerivedType(typeof(StructureDetailsJob), "structureDetails")]
[JsonDerivedType(typeof(LandmarksJob), "landmarks")]
[JsonDerivedType(typeof(EndGatewaysJob), "endGateways")]
[JsonDerivedType(typeof(SlimeAreaJob), "slimeArea")]
[JsonDerivedType(typeof(SelfTestJob), "selfTest")]
[JsonDerivedType(typeof(SearchBatchJob), "searchBatch")]
[JsonDerivedType(typeof(QuadHutBaseScanJob), "quadHutBaseScan")]
public abstract record ComputeJob;

public abstract record ComputeJob<TResult> : ComputeJob;

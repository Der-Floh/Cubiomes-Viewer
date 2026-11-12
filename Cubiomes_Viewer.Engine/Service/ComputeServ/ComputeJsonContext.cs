using System.Text.Json.Serialization;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.SelfTests;
using Cubiomes_Viewer.Engine.Data.Structures;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

/// <summary>Serialization metadata for every job and every job result that can cross to a worker in another runtime.</summary>
[JsonSerializable(typeof(ComputeJob))]
[JsonSerializable(typeof(TileData))]
[JsonSerializable(typeof(StructureAreaData))]
[JsonSerializable(typeof(StructureDetails))]
[JsonSerializable(typeof(WorldLandmarks))]
[JsonSerializable(typeof(EndGatewayData))]
[JsonSerializable(typeof(SlimeAreaData))]
[JsonSerializable(typeof(SelfTestReport))]
[JsonSerializable(typeof(SearchBatchResult))]
[JsonSerializable(typeof(QuadHutBaseScan))]
[JsonSerializable(typeof(WorkerError))]
internal sealed partial class ComputeJsonContext : JsonSerializerContext;

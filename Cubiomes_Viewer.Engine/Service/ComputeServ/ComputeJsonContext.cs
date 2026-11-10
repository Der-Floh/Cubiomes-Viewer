using System.Text.Json.Serialization;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.SelfTests;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

/// <summary>Serialization metadata for every job and every job result that can cross to a worker in another runtime.</summary>
[JsonSerializable(typeof(ComputeJob))]
[JsonSerializable(typeof(TileData))]
[JsonSerializable(typeof(SelfTestReport))]
[JsonSerializable(typeof(WorkerError))]
internal sealed partial class ComputeJsonContext : JsonSerializerContext;

using Cubiomes_Viewer.Engine.Data.SelfTests;

namespace Cubiomes_Viewer.Engine.Service.SelfTestServ;

public interface ITileBenchmarkService
{
    /// <summary>Generates a block of biome tiles through the compute service and measures the throughput.</summary>
    Task<TileBenchmarkResult> RunAsync(CancellationToken cancellationToken = default);
}

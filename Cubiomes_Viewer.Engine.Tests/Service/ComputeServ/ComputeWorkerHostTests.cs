using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.SelfTests;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Interop;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.TileServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Engine.Tests.Service.ComputeServ;

public sealed class ComputeWorkerHostTests : IDisposable
{
    private readonly ServiceProvider _services = new ServiceCollection().AddLogging().AddCubiomesEngine().BuildServiceProvider();
    private readonly ComputeWorkerHost _host;

    public ComputeWorkerHostTests()
    {
        _host = _services.GetRequiredService<ComputeWorkerHost>();
    }

    public void Dispose() => _services.Dispose();

    [Fact]
    public void SerializeJob_BiomeTileJob_RoundTripsThroughTheEnvelope()
    {
        var job = CreateTileJob(scale: 16);

        var restored = ComputeEnvelope.DeserializeJob(ComputeEnvelope.SerializeJob(job));

        Assert.Equal(job, restored);
    }

    [Fact]
    public void Execute_BiomeTileJob_ReturnsTheSameTileAsTheHandler()
    {
        var job = CreateTileJob(scale: 4);
        using var context = new WorkerContext(new Engine.Service.WorldGenServ.CubiomesWorldGeneratorFactory());
        var expected = new BiomeTileJobHandler().Handle(job, context, CancellationToken.None);

        var response = _host.Execute(ComputeEnvelope.SerializeJob(job));
        var tile = ComputeEnvelope.DeserializeResponse<TileData>(response);

        Assert.Equal(job.Request, tile.Request);
        Assert.Equal(job.Samples, tile.Samples);
        Assert.Equal(expected.Biomes, tile.Biomes);
    }

    [Fact]
    public void Execute_SelfTestJob_ReturnsPassingReport()
    {
        var response = _host.Execute(ComputeEnvelope.SerializeJob(new SelfTestJob()));

        var report = ComputeEnvelope.DeserializeResponse<SelfTestReport>(response);

        Assert.True(report.Passed, string.Join(Environment.NewLine, report.Checks.Where(check => !check.Passed)));
        Assert.NotEmpty(report.Checks);
    }

    [Fact]
    public void Execute_JobTheGeneratorRejects_ReportsTheNativeStatusToTheCaller()
    {
        var world = new WorldKey(1, McVersion.V1_12_2, Dimension.Overworld, GeneratorFlags.None);
        var job = new BiomeTileJob(new TileRequest(world, LayerMode.Biomes, 64, new TileKey(1024, 0, 0)), 256);

        var response = _host.Execute(ComputeEnvelope.SerializeJob(job));

        var exception = Assert.Throws<CubiomesNativeException>(() => ComputeEnvelope.DeserializeResponse<TileData>(response));
        Assert.Equal(NativeStatus.Unsupported, exception.Status);
    }

    [Fact]
    public void Execute_MalformedRequest_ReportsAFailureInsteadOfThrowing()
    {
        var response = _host.Execute("{ not a job"u8);

        Assert.Throws<ComputeJobException>(() => ComputeEnvelope.DeserializeResponse<TileData>(response));
    }

    private static BiomeTileJob CreateTileJob(int scale)
    {
        var world = new WorldKey(-262, McVersion.V1_21_3, Dimension.Overworld, GeneratorFlags.LargeBiomes);
        return new BiomeTileJob(new TileRequest(world, LayerMode.Biomes, 70, new TileKey(scale, -3, 2)), TileMetrics.Noise.TileSamples);
    }
}

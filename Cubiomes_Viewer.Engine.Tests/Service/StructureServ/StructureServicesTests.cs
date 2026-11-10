using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Engine.Tests.Service.StructureServ;

public sealed class StructureServicesTests : IDisposable
{
    private const long Seed = 262;
    private static readonly TimeSpan _timeout = TimeSpan.FromSeconds(30);
    private static readonly WorldKey _overworld = new(Seed, McVersion.V1_21_3, Dimension.Overworld, GeneratorFlags.None);
    private static readonly MapViewport _viewport = new(new MapCamera(300, -700, 8), 1280, 800);

    private readonly ServiceProvider _services = new ServiceCollection()
        .AddLogging()
        .AddCubiomesEngine()
        .AddSingleton<IComputeService, InlineComputeService>()
        .BuildServiceProvider();

    public void Dispose() => _services.Dispose();

    [Fact]
    public async Task StructureAreas_VisibleAreasLoaded_HoldEveryStructureInTheViewport()
    {
        var service = _services.GetRequiredService<IStructureAreaService>();
        var requests = service.GetVisibleAreas(_overworld, StructureType.Village, _viewport);

        service.SetVisibleAreas(requests);
        await WaitUntilAsync(() => service.PendingCount == 0);

        var loaded = requests.SelectMany(request => service.TryGetStructures(request, out var area) ? area.Positions : []).ToHashSet();
        var viewportArea = new BlockArea((int)_viewport.Left, (int)_viewport.Top, (int)(_viewport.Right - _viewport.Left), (int)(_viewport.Bottom - _viewport.Top));
        using var generator = new CubiomesWorldGenerator(_overworld.Version, _overworld.Flags);
        generator.Apply(_overworld.Dimension, _overworld.Seed);
        var expected = generator.FindStructures(StructureType.Village, viewportArea);

        Assert.NotEmpty(expected);
        Assert.Subset(loaded, expected.ToHashSet());
        Assert.All(requests, request => Assert.True(service.TryGetStructures(request, out _)));
    }

    [Fact]
    public void StructureAreas_NeighbouringAreas_TileTheViewportWithoutOverlap()
    {
        var service = _services.GetRequiredService<IStructureAreaService>();

        var areas = service.GetVisibleAreas(_overworld, StructureType.Mansion, _viewport).Select(request => request.Area).ToArray();

        Assert.NotEmpty(areas);
        Assert.Equal(areas.Length, areas.Distinct().Count());
        Assert.All(areas, area => Assert.Equal(0, area.X % area.SizeX));
        Assert.True(areas.Min(area => area.X) <= _viewport.Left && areas.Max(area => area.Right) >= _viewport.Right);
        Assert.True(areas.Min(area => area.Z) <= _viewport.Top && areas.Max(area => area.Bottom) >= _viewport.Bottom);
    }

    [Theory]
    [InlineData(StructureType.Fortress)]
    [InlineData(StructureType.EndCity)]
    public void StructureAreas_TypeOfAnotherDimension_HasNoVisibleAreas(StructureType type)
        => Assert.Empty(_services.GetRequiredService<IStructureAreaService>().GetVisibleAreas(_overworld, type, _viewport));

    [Fact]
    public void StructureAreas_TypeNotInVersion_HasNoVisibleAreas()
    {
        var oldWorld = _overworld with { Version = McVersion.V1_12_2 };

        Assert.Empty(_services.GetRequiredService<IStructureAreaService>().GetVisibleAreas(oldWorld, StructureType.TrialChambers, _viewport));
    }

    [Fact]
    public async Task Landmarks_ShownWorld_ProvidesSpawnAndStrongholdsForAnyDimensionOfIt()
    {
        var service = _services.GetRequiredService<IWorldLandmarkService>();
        var nether = _overworld with { Dimension = Dimension.Nether };

        service.Show(nether);
        await WaitUntilAsync(() => service.TryGetLandmarks(_overworld, out _));

        Assert.True(service.TryGetLandmarks(nether, out var landmarks));
        Assert.NotNull(landmarks.Spawn);
        Assert.Equal(128, landmarks.Strongholds.Length);
    }

    [Fact]
    public async Task SlimeAreas_VisibleAreasLoaded_MatchTheSlimeChunkFinder()
    {
        var service = _services.GetRequiredService<ISlimeAreaService>();
        var viewport = new MapViewport(new MapCamera(-100, 250, 2), 1280, 800);
        var requests = service.GetVisibleAreas(Seed, viewport);

        service.SetVisibleAreas(requests);
        await WaitUntilAsync(() => requests.All(request => service.TryGetSlimeChunks(request, out _)));

        var request = requests[0];
        Assert.True(service.TryGetSlimeChunks(request, out var area));
        var expected = new byte[request.SizeChunks * request.SizeChunks];
        new CubiomesSlimeChunkFinder().FindSlimeChunks(Seed, request.ChunkX, request.ChunkZ, request.SizeChunks, request.SizeChunks, expected);
        Assert.Equal(expected, area.Chunks);
        Assert.Equal(expected[5 * request.SizeChunks + 3] == 1, area.IsSlimeChunk(3, 5));
    }

    [Fact]
    public void WorkerHost_StructureJobs_ReturnTheSameDataAsTheirHandlers()
    {
        var host = _services.GetRequiredService<ComputeWorkerHost>();
        using var context = new WorkerContext(new CubiomesWorldGeneratorFactory());
        var areaJob = new StructureAreaJob(new StructureAreaRequest(_overworld, StructureType.RuinedPortal, new BlockArea(-2048, -2048, 4096, 4096)));
        var landmarksJob = new LandmarksJob(_overworld);
        var slimeJob = new SlimeAreaJob(new SlimeAreaRequest(Seed, -8, 4, 16));

        var area = ComputeEnvelope.DeserializeResponse<StructureAreaData>(host.Execute(ComputeEnvelope.SerializeJob(areaJob)));
        var landmarks = ComputeEnvelope.DeserializeResponse<WorldLandmarks>(host.Execute(ComputeEnvelope.SerializeJob(landmarksJob)));
        var slime = ComputeEnvelope.DeserializeResponse<SlimeAreaData>(host.Execute(ComputeEnvelope.SerializeJob(slimeJob)));

        var expectedArea = new StructureAreaJobHandler().Handle(areaJob, context, CancellationToken.None);
        var expectedLandmarks = new LandmarksJobHandler().Handle(landmarksJob, context, CancellationToken.None);
        var expectedSlime = new SlimeAreaJobHandler(new CubiomesSlimeChunkFinder()).Handle(slimeJob, context, CancellationToken.None);
        Assert.Equal(areaJob.Request, area.Request);
        Assert.NotEmpty(area.Positions);
        Assert.Equal(expectedArea.Positions, area.Positions);
        Assert.Equal(_overworld, landmarks.World);
        Assert.Equal(expectedLandmarks.Spawn, landmarks.Spawn);
        Assert.Equal(expectedLandmarks.Strongholds, landmarks.Strongholds);
        Assert.Equal(slimeJob.Request, slime.Request);
        Assert.Equal(expectedSlime.Chunks, slime.Chunks);
    }

    [Fact]
    public void WorkerHost_DetailAndGatewayJobs_ReturnTheSameDataAsTheirHandlers()
    {
        var host = _services.GetRequiredService<ComputeWorkerHost>();
        using var context = new WorkerContext(new CubiomesWorldGeneratorFactory());
        var end = _overworld with { Dimension = Dimension.End };
        var searchArea = new BlockArea(-4096, -4096, 8192, 8192);
        var city = context.GetGenerator(end).FindStructures(StructureType.EndCity, searchArea)[0];
        var village = context.GetGenerator(_overworld).FindStructures(StructureType.Village, searchArea)[0];
        var cityJob = new StructureDetailsJob(end, StructureType.EndCity, city);
        var villageJob = new StructureDetailsJob(_overworld, StructureType.Village, village);
        var gatewaysJob = new EndGatewaysJob(_overworld);

        var cityDetails = ComputeEnvelope.DeserializeResponse<StructureDetails>(host.Execute(ComputeEnvelope.SerializeJob(cityJob)));
        var villageDetails = ComputeEnvelope.DeserializeResponse<StructureDetails>(host.Execute(ComputeEnvelope.SerializeJob(villageJob)));
        var gateways = ComputeEnvelope.DeserializeResponse<EndGatewayData>(host.Execute(ComputeEnvelope.SerializeJob(gatewaysJob)));

        var expectedCity = new StructureDetailsJobHandler().Handle(cityJob, context, CancellationToken.None);
        var expectedVillage = new StructureDetailsJobHandler().Handle(villageJob, context, CancellationToken.None);
        var expectedGateways = new EndGatewaysJobHandler().Handle(gatewaysJob, context, CancellationToken.None);
        Assert.Equal(city, cityDetails.Position);
        Assert.Null(cityDetails.Variant);
        Assert.NotEmpty(cityDetails.Pieces);
        Assert.Equal(expectedCity.Pieces, cityDetails.Pieces);
        Assert.NotNull(villageDetails.Variant);
        Assert.Equal(expectedVillage.Variant, villageDetails.Variant);
        Assert.Empty(villageDetails.Pieces);
        Assert.Equal(_overworld, gateways.World);
        Assert.Equal(20, gateways.Gateways.Length);
        Assert.Equal(expectedGateways.Gateways, gateways.Gateways);
    }

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow + _timeout;
        while (!condition())
        {
            Assert.True(DateTime.UtcNow < deadline, "The data was not loaded in time.");
            await Task.Delay(5, TestContext.Current.CancellationToken);
        }
    }
}

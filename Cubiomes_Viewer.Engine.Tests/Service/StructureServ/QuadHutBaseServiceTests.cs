using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace Cubiomes_Viewer.Engine.Tests.Service.StructureServ;

/// <summary>Finds every quad-hut base there is once, which takes about ten seconds on all processors.</summary>
public sealed class QuadHutBaseServiceTests : IClassFixture<QuadHutBaseServiceTests.AllBases>
{
    private const int IdealBases = 74_474;
    private const int ClassicBases = 33_485;
    private const int NormalBases = 185_757;
    private const int BarelyBases = 462_029;

    private readonly AllBases _all;

    public QuadHutBaseServiceTests(AllBases all)
    {
        _all = all;
    }

    [Fact]
    public void GetBasesAsync_EveryClass_FindsTheKnownTotals()
    {
        Assert.Equal(IdealBases, _all.UpTo[QuadQuality.Ideal].Count);
        Assert.Equal(IdealBases + ClassicBases, _all.UpTo[QuadQuality.Classic].Count);
        Assert.Equal(IdealBases + ClassicBases + NormalBases, _all.UpTo[QuadQuality.Normal].Count);
        Assert.Equal(IdealBases + ClassicBases + NormalBases + BarelyBases, _all.UpTo[QuadQuality.Barely].Count);
    }

    [Fact]
    public void GetBasesAsync_EveryBase_IsOneAndListedWithTheBestClassFirst()
    {
        var finder = new CubiomesQuadFinder();
        var qualities = _all.UpTo[QuadQuality.Barely].Select(value => Assert.NotNull(finder.DescribeQuadHutBase(value)).Quality).ToList();

        Assert.Equal(qualities.Order(), qualities);
        Assert.Equal(IdealBases, qualities.Count(quality => quality == QuadQuality.Ideal));
        Assert.Equal(BarelyBases, qualities.Count(quality => quality == QuadQuality.Barely));
        Assert.Equal(qualities.Count, _all.UpTo[QuadQuality.Barely].Distinct().Count());
    }

    [Fact]
    public async Task GetBasesAsync_StoredBefore_ReadsThemWithoutScanningAgain()
    {
        var service = new QuadHutBaseService(new NoComputeService(), new CubiomesQuadFinder(), _all.Store, NullLogger<QuadHutBaseService>.Instance);

        var bases = await service.GetBasesAsync(QuadQuality.Normal, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(_all.UpTo[QuadQuality.Normal], bases);
    }

    [Fact]
    public async Task GetBasesAsync_ClassThatIsNone_Throws()
        => await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => _all.Service.GetBasesAsync(QuadQuality.Other, cancellationToken: TestContext.Current.CancellationToken));

    /// <summary>Every class, found once with the real workers and kept in a temporary store.</summary>
    public sealed class AllBases : IAsyncLifetime
    {
        private readonly string _directory = Path.Combine(Path.GetTempPath(), "cubiomes-viewer-tests", Guid.NewGuid().ToString("N"));
        private ServiceProvider? _services;

        public FileSystemAppDataStore Store { get; }
        public IQuadHutBaseService Service => _services!.GetRequiredService<IQuadHutBaseService>();
        public Dictionary<QuadQuality, IReadOnlyList<ulong>> UpTo { get; } = [];
        public List<double> Progress { get; } = [];

        public AllBases()
        {
            Store = new FileSystemAppDataStore(_directory);
        }

        public async ValueTask InitializeAsync()
        {
            _services = new ServiceCollection().AddLogging().AddCubiomesEngine().AddSingleton<IAppDataStore>(Store).BuildServiceProvider();
            foreach (var quality in new[] { QuadQuality.Ideal, QuadQuality.Classic, QuadQuality.Normal, QuadQuality.Barely })
                UpTo[quality] = await Service.GetBasesAsync(quality, new SynchronousProgress(Progress));
        }

        public ValueTask DisposeAsync()
        {
            _services?.Dispose();
            if (Directory.Exists(_directory))
                Directory.Delete(_directory, recursive: true);
            return ValueTask.CompletedTask;
        }
    }

    private sealed class SynchronousProgress(List<double> reports) : IProgress<double>
    {
        public void Report(double value)
        {
            lock (reports)
                reports.Add(value);
        }
    }

    private sealed class NoComputeService : IComputeService
    {
        public int WorkerCount => 1;

        public Task<TResult> RunAsync<TResult>(ComputeJob<TResult> job, JobPriority priority, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("Stored bases must not be scanned again.");
    }
}

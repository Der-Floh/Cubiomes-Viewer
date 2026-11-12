using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Repository.SessionRepo;
using Cubiomes_Viewer.Engine.Service.SearchServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Engine.Tests.Service.SearchServ;

public sealed class SearchSessionServiceTests : IDisposable
{
    private static readonly TimeSpan _saveTimeout = TimeSpan.FromSeconds(10);
    private static readonly SearchSetup _setup = new() { Definition = SearchRuns.SlimeChunks, Source = new SeedRangeSource { FirstSeed = -40_000, LastSeed = 59_999 }, MaxResults = 10_000 };

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cubiomes-viewer-tests", Guid.NewGuid().ToString("N"));

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public void LoadAsync_AfterTheAppClosedDuringASearch_ContinuesItAsIfNothingHappened() => SingleThread.Run(async () =>
    {
        var expected = await SearchWithoutStoppingAsync(_setup);
        SearchSnapshot stopped;
        using (var closing = CreateServices(new FileSystemAppDataStore(_directory)))
        {
            var sessions = closing.GetRequiredService<ISearchSessionService>();
            await sessions.LoadAsync(TestContext.Current.CancellationToken);
            sessions.Setup = _setup;
            await SearchRuns.StartAndStopAtTheFirstProgressAsync(closing.GetRequiredService<ISeedSearchService>(), _setup);
            stopped = Assert.IsType<SearchSnapshot>(sessions.CreateSession().Search);
            await sessions.FlushAsync(TestContext.Current.CancellationToken);
        }

        using var reopened = CreateServices(new FileSystemAppDataStore(_directory));
        var restored = reopened.GetRequiredService<ISearchSessionService>();
        var search = reopened.GetRequiredService<ISeedSearchService>();
        await restored.LoadAsync(TestContext.Current.CancellationToken);
        var paused = (search.State, search.Progress.NextSeed, Results: search.Results.ToList());
        await search.ResumeAsync(TestContext.Current.CancellationToken);

        Assert.Equal(_setup, restored.Setup);
        Assert.Equal((SearchState.Paused, stopped.NextSeed), (paused.State, paused.NextSeed));
        Assert.Equal(stopped.Results, paused.Results);
        Assert.Equal(expected, search.Results);
        Assert.NotEmpty(expected);
    });

    [Fact]
    public async Task LoadAsync_NothingStored_StartsWithAnEmptySetupAndNoSearch()
    {
        using var services = CreateServices(new FakeRepository());
        var sessions = services.GetRequiredService<ISearchSessionService>();

        await sessions.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(sessions.Setup.Definition.Conditions);
        Assert.Null(sessions.CreateSession().Search);
        Assert.Equal(SearchState.Idle, services.GetRequiredService<ISeedSearchService>().State);
    }

    [Fact]
    public async Task LoadAsync_StorageCannotBeRead_NeverOverwritesIt()
    {
        var repository = new FakeRepository { LoadFailure = new IOException("The storage is not available.") };
        using var services = CreateServices(repository);
        var sessions = services.GetRequiredService<ISearchSessionService>();

        await sessions.LoadAsync(TestContext.Current.CancellationToken);
        sessions.Setup = _setup;
        await sessions.FlushAsync(TestContext.Current.CancellationToken);

        Assert.Empty(repository.Saved);
    }

    [Fact]
    public async Task LoadAsync_StoredSearchThatCannotContinue_KeepsOnlyTheSetup()
    {
        var broken = new SearchSnapshot { Setup = _setup with { Definition = new SearchDefinition() }, Scanned = 5, Results = [] };
        using var services = CreateServices(new FakeRepository { Stored = new SearchSession { Setup = _setup, Search = broken } });
        var sessions = services.GetRequiredService<ISearchSessionService>();

        await sessions.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(_setup, sessions.Setup);
        Assert.Equal(SearchState.Idle, services.GetRequiredService<ISeedSearchService>().State);
    }

    [Fact]
    public async Task FlushAsync_AfterASearch_WritesTheSetupAndTheSearch()
    {
        var repository = new FakeRepository();
        using var services = CreateServices(repository);
        var sessions = services.GetRequiredService<ISearchSessionService>();
        var search = services.GetRequiredService<ISeedSearchService>();
        await sessions.LoadAsync(TestContext.Current.CancellationToken);

        sessions.Setup = _setup with { MaxResults = 2 };
        await search.StartAsync(sessions.Setup, TestContext.Current.CancellationToken);
        await sessions.FlushAsync(TestContext.Current.CancellationToken);

        var saved = repository.Saved[^1];
        Assert.Equal(sessions.Setup, saved.Setup);
        Assert.Equal(search.CreateSnapshot(), saved.Search);
        Assert.True(saved.Search?.IsFinished);
    }

    [Fact]
    public async Task Setup_Changed_IsStoredShortlyAfter()
    {
        var repository = new FakeRepository();
        using var services = CreateServices(repository);
        var sessions = services.GetRequiredService<ISearchSessionService>();
        await sessions.LoadAsync(TestContext.Current.CancellationToken);

        sessions.Setup = _setup with { MaxResults = 3 };
        sessions.Setup = _setup;
        var saved = await repository.NextSave.WaitAsync(_saveTimeout, TestContext.Current.CancellationToken);

        Assert.Equal(_setup, saved.Setup);
        Assert.Single(repository.Saved);
    }

    [Fact]
    public async Task Open_Session_ReplacesTheSetupAndTheSearch()
    {
        using var services = CreateServices(new FakeRepository());
        var sessions = services.GetRequiredService<ISearchSessionService>();
        var search = services.GetRequiredService<ISeedSearchService>();
        await sessions.LoadAsync(TestContext.Current.CancellationToken);
        var finished = new SearchSnapshot { Setup = _setup, Scanned = 12, Results = [3, 11], IsFinished = true };

        sessions.Open(new SearchSession { Setup = _setup with { Workers = 1 }, Search = finished });

        Assert.Equal(_setup with { Workers = 1 }, sessions.Setup);
        Assert.Equal(SearchState.Finished, search.State);
        Assert.Equal([3, 11], search.Results);
    }

    [Fact]
    public async Task Open_SessionWithoutASearch_ForgetsTheLastSearch()
    {
        using var services = CreateServices(new FakeRepository());
        var sessions = services.GetRequiredService<ISearchSessionService>();
        var search = services.GetRequiredService<ISeedSearchService>();
        await sessions.LoadAsync(TestContext.Current.CancellationToken);
        await search.StartAsync(_setup with { MaxResults = 1 }, TestContext.Current.CancellationToken);

        sessions.Open(new SearchSession { Setup = _setup });

        Assert.Equal(SearchState.Idle, search.State);
        Assert.Empty(search.Results);
    }

    [Fact]
    public async Task Open_WhileASearchIsRunning_Throws()
    {
        using var services = CreateServices(new FakeRepository());
        var sessions = services.GetRequiredService<ISearchSessionService>();
        var search = services.GetRequiredService<ISeedSearchService>();
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
        await sessions.LoadAsync(TestContext.Current.CancellationToken);

        var running = search.StartAsync(new SearchSetup { Definition = SearchRuns.Never }, stopping.Token);
        Assert.Throws<InvalidOperationException>(() => sessions.Open(new SearchSession { Setup = _setup }));
        await stopping.CancelAsync();
        await running;

        Assert.Equal(SearchState.Paused, search.State);
    }

    [Fact]
    public async Task Import_ExportedSession_OpensTheSameSetupAndSearchElsewhere()
    {
        using var exporting = CreateServices(new FileSystemAppDataStore(_directory));
        var source = exporting.GetRequiredService<ISearchSessionService>();
        await source.LoadAsync(TestContext.Current.CancellationToken);
        source.Setup = _setup;
        await exporting.GetRequiredService<ISeedSearchService>().StartAsync(_setup with { MaxResults = 2 }, TestContext.Current.CancellationToken);
        var exported = source.Export();

        using var importing = CreateServices(new FileSystemAppDataStore(Path.Combine(_directory, "other")));
        var target = importing.GetRequiredService<ISearchSessionService>();
        await target.LoadAsync(TestContext.Current.CancellationToken);
        target.Import(exported);

        Assert.Equal(source.CreateSession(), target.CreateSession());
        Assert.Equal(SearchState.Finished, importing.GetRequiredService<ISeedSearchService>().State);
    }

    [Fact]
    public async Task Import_ContentThatIsNoSession_ThrowsAndKeepsTheSession()
    {
        using var services = CreateServices(new FileSystemAppDataStore(_directory));
        var sessions = services.GetRequiredService<ISearchSessionService>();
        await sessions.LoadAsync(TestContext.Current.CancellationToken);
        sessions.Setup = _setup;

        Assert.Throws<FormatException>(() => sessions.Import("{ not json"u8));
        Assert.Equal(_setup, sessions.Setup);
    }

    private static async Task<IReadOnlyList<long>> SearchWithoutStoppingAsync(SearchSetup setup)
    {
        using var services = CreateServices(new FakeRepository());
        var search = services.GetRequiredService<ISeedSearchService>();
        await search.StartAsync(setup, TestContext.Current.CancellationToken);
        return [.. search.Results];
    }

    private static ServiceProvider CreateServices(IAppDataStore store)
        => new ServiceCollection().AddLogging().AddCubiomesEngine().AddSingleton(store).BuildServiceProvider();

    private static ServiceProvider CreateServices(ISearchSessionRepository repository)
        => new ServiceCollection().AddLogging().AddCubiomesEngine().AddSingleton(repository).BuildServiceProvider();

    private sealed class FakeRepository : ISearchSessionRepository
    {
        public SearchSession? Stored { get; init; }
        public Exception? LoadFailure { get; init; }
        public List<SearchSession> Saved { get; } = [];
        public Task<SearchSession> NextSave => _nextSave.Task;

        private readonly TaskCompletionSource<SearchSession> _nextSave = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<SearchSession?> LoadAsync(CancellationToken cancellationToken = default)
            => LoadFailure is null ? Task.FromResult(Stored) : Task.FromException<SearchSession?>(LoadFailure);

        public Task SaveAsync(SearchSession session, CancellationToken cancellationToken = default)
        {
            lock (Saved)
                Saved.Add(session);
            _nextSave.TrySetResult(session);
            return Task.CompletedTask;
        }

        public byte[] Serialize(SearchSession session) => throw new NotSupportedException("The session service stores sessions without writing files.");

        public SearchSession Deserialize(ReadOnlySpan<byte> content) => throw new NotSupportedException("The session service stores sessions without reading files.");
    }
}

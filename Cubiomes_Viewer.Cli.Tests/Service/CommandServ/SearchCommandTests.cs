using Cubiomes_Viewer.Cli.Data;
using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Cli.Tests.Service.CommandServ;

/// <summary>The CLI has to find what the app finds in the same session file, however often it is stopped on the way.</summary>
public sealed class SearchCommandTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cubiomes-viewer-tests", Guid.NewGuid().ToString("N"));

    private string AppData => Path.Combine(_directory, "app-data");

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public SearchCommandTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    public static TheoryData<string> Fixtures() => [.. FixtureSessions.Names];

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task Fixture_RunToTheEnd_WritesTheSeedsTheAppFinds(string name)
    {
        var session = FixtureSessions.CopyTo(name, _directory);
        var seedFile = Path.Combine(_directory, "seeds.txt");
        var app = await AppSearch.RunAsync(FixtureSessions.GetPath(name), AppData, Token);

        var result = await CliRun.RunAsync(AppData, ["--session", session, "--out", seedFile], cancellationToken: Token);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Equal(SearchState.Finished, app.State);
        Assert.NotEmpty(app.Seeds);
        Assert.Equal(app.Seeds, FixtureSessions.ParseSeeds(await File.ReadAllTextAsync(seedFile, Token)));
        Assert.Empty(result.Output);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public async Task Fixture_RunToTheEnd_SavesTheFinishedSearchIntoTheSession(string name)
    {
        var session = FixtureSessions.CopyTo(name, _directory);
        var app = await AppSearch.RunAsync(FixtureSessions.GetPath(name), AppData, Token);

        await CliRun.RunAsync(AppData, ["--session", session], cancellationToken: Token);

        var saved = FixtureSessions.Read(session);
        Assert.Equal(FixtureSessions.Read(FixtureSessions.GetPath(name)).Setup, saved.Setup);
        Assert.NotNull(saved.Search);
        Assert.True(saved.Search.IsFinished);
        Assert.Equal(saved.Setup, saved.Search.Setup);
        Assert.Equal(app.Seeds, saved.Search.Results);
    }

    [Theory]
    [MemberData(nameof(Fixtures))]
    public void Fixture_WrittenAgain_IsTheSameFile(string name)
    {
        var path = FixtureSessions.GetPath(name);

        var written = FixtureSessions.Serialize(FixtureSessions.Read(path));

        Assert.Equal(File.ReadAllBytes(path), written);
    }

    [Fact]
    public async Task NoSeedFile_WritesTheSeedsToTheOutput()
    {
        var session = FixtureSessions.CopyTo(FixtureSessions.SlimeChunks, _directory);
        var app = await AppSearch.RunAsync(FixtureSessions.GetPath(FixtureSessions.SlimeChunks), AppData, Token);

        var result = await CliRun.RunAsync(AppData, ["--session", session], cancellationToken: Token);

        Assert.Equal(app.Seeds, result.Seeds);
        Assert.Contains("Finished: found 100 seeds", result.Messages);
    }

    [Fact]
    public async Task StoppedRun_RunAgain_EndsWithTheSeedsOfAnUninterruptedRun()
    {
        var session = FixtureSessions.CopyTo(FixtureSessions.SlimeChunks, _directory);
        var app = await AppSearch.RunAsync(FixtureSessions.GetPath(FixtureSessions.SlimeChunks), AppData, Token);

        var stopped = await RunUntilTheFirstSeedAsync(session);
        var continued = await CliRun.RunAsync(AppData, ["--session", session], cancellationToken: Token);

        Assert.Equal(ExitCodes.Stopped, stopped.ExitCode);
        Assert.InRange(stopped.Seeds.Count, 1, app.Seeds.Count - 1);
        Assert.Equal(app.Seeds.Take(stopped.Seeds.Count), stopped.Seeds);
        Assert.Contains("Stopped before seed", stopped.Messages);
        Assert.Equal(ExitCodes.Success, continued.ExitCode);
        Assert.Contains("Continuing before seed", continued.Messages);
        Assert.Equal(app.Seeds, continued.Seeds);
    }

    [Fact]
    public async Task StoppedRun_SavedSession_TheAppContinuesItToTheSameSeeds()
    {
        var session = FixtureSessions.CopyTo(FixtureSessions.SlimeChunks, _directory);
        var uninterrupted = await AppSearch.RunAsync(FixtureSessions.GetPath(FixtureSessions.SlimeChunks), AppData, Token);

        await RunUntilTheFirstSeedAsync(session);
        var continued = await AppSearch.RunAsync(session, AppData, Token);

        Assert.True(continued.Continued);
        Assert.Equal(uninterrupted.Seeds, continued.Seeds);
    }

    [Fact]
    public async Task FinishedSession_ListsItsSeedsWithoutSearchingAgain()
    {
        var session = FixtureSessions.CopyTo(FixtureSessions.SlimeChunks, _directory);
        var first = await CliRun.RunAsync(AppData, ["--session", session], cancellationToken: Token);
        var saved = await File.ReadAllBytesAsync(session, Token);

        var second = await CliRun.RunAsync(AppData, ["--session", session], cancellationToken: Token);

        Assert.Equal(ExitCodes.Success, second.ExitCode);
        Assert.Equal(first.Seeds, second.Seeds);
        Assert.Contains("--reset", second.Messages);
        Assert.Equal(saved, await File.ReadAllBytesAsync(session, Token));
    }

    [Fact]
    public async Task StoppedSession_KeepsTheSeedsItFoundAndContinuesBehindThem()
    {
        var setup = FixtureSessions.Read(FixtureSessions.GetPath(FixtureSessions.SlimeChunks)).Setup;
        var session = WriteStoppedSession(setup, setup);
        var app = await AppSearch.RunAsync(FixtureSessions.GetPath(FixtureSessions.SlimeChunks), AppData, Token);

        var result = await CliRun.RunAsync(AppData, ["--session", session], cancellationToken: Token);

        long[] expected = [0, 1, 2, .. app.Seeds.Take(setup.MaxResults - 3)];
        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Equal(expected, result.Seeds);
    }

    [Fact]
    public async Task Reset_StoppedSession_StartsOver()
    {
        var setup = FixtureSessions.Read(FixtureSessions.GetPath(FixtureSessions.SlimeChunks)).Setup;
        var session = WriteStoppedSession(setup, setup);
        var app = await AppSearch.RunAsync(FixtureSessions.GetPath(FixtureSessions.SlimeChunks), AppData, Token);

        var result = await CliRun.RunAsync(AppData, ["--session", session, "--reset"], cancellationToken: Token);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Equal(app.Seeds, result.Seeds);
    }

    [Fact]
    public async Task SetupChangedSinceTheStoppedSearch_StartsOverWithTheSetup()
    {
        var setup = FixtureSessions.Read(FixtureSessions.GetPath(FixtureSessions.SlimeChunks)).Setup;
        var session = WriteStoppedSession(setup, setup with { MaxResults = 5 });
        var app = await AppSearch.RunAsync(FixtureSessions.GetPath(FixtureSessions.SlimeChunks), AppData, Token);

        var result = await CliRun.RunAsync(AppData, ["--session", session], cancellationToken: Token);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.Contains("setup changed", result.Messages);
        Assert.Equal(app.Seeds, result.Seeds);
    }

    [Fact]
    public async Task SessionWithoutConditions_FailsAndLeavesTheSeedFileAlone()
    {
        var session = FixtureSessions.Write(new SearchSession { Setup = new SearchSetup { Definition = new SearchDefinition() } }, _directory);
        var seedFile = Path.Combine(_directory, "seeds.txt");
        await File.WriteAllTextAsync(seedFile, "42", Token);

        var result = await CliRun.RunAsync(AppData, ["--session", session, "--out", seedFile], cancellationToken: Token);

        Assert.Equal(ExitCodes.Failure, result.ExitCode);
        Assert.Contains("It has no conditions.", result.Messages);
        Assert.Equal("42", await File.ReadAllTextAsync(seedFile, Token));
    }

    [Fact]
    public async Task FileThatIsNoSession_FailsWithoutSearching()
    {
        var session = Path.Combine(_directory, "notes.json");
        await File.WriteAllTextAsync(session, """{ "notes": [] }""", Token);

        var result = await CliRun.RunAsync(AppData, ["--session", session], cancellationToken: Token);

        Assert.Equal(ExitCodes.Failure, result.ExitCode);
        Assert.Contains("This is not a search session of Cubiomes Viewer.", result.Messages);
        Assert.Empty(result.Output);
    }

    [Fact]
    public async Task MissingSession_FailsWithAMessage()
    {
        var session = Path.Combine(_directory, "missing.json");

        var result = await CliRun.RunAsync(AppData, ["--session", session], cancellationToken: Token);

        Assert.Equal(ExitCodes.Failure, result.ExitCode);
        Assert.Contains("missing.json", result.Messages);
    }

    private async Task<CliResult> RunUntilTheFirstSeedAsync(string session)
    {
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(Token);
        return await CliRun.RunAsync(AppData, ["--session", session], new StoppingWriter(stopping), stopping.Token);
    }

    /// <summary>A session whose search was stopped after finding the seeds 0, 1 and 2 among the first three seeds.</summary>
    private string WriteStoppedSession(SearchSetup setup, SearchSetup searchedSetup)
    {
        var search = new SearchSnapshot { Setup = searchedSetup, Scanned = 3, NextSeed = 3, Results = [0, 1, 2] };
        return FixtureSessions.Write(new SearchSession { Setup = setup, Search = search }, _directory);
    }
}

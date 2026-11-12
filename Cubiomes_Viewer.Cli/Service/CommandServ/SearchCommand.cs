using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Cubiomes_Viewer.Cli.Data;
using Cubiomes_Viewer.Cli.Repository.FileRepo;
using Cubiomes_Viewer.Cli.Service.ConsoleServ;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Service.ConditionServ;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Cli.Service.CommandServ;

/// <summary>
/// Runs the search of a session file as the app's Search panel does once the file is open: a stopped search with the
/// same setup continues, any other one starts over. Seeds are written as they are found, and the progress is saved
/// back into the session file every few seconds and when the search stops.
/// </summary>
public sealed class SearchCommand : ISearchCommand
{
    private const string SetupChangedMessage = "The setup changed since the last search, so the search starts over.";
    private const string ContinueHint = "Run the session again to continue.";

    private static readonly TimeSpan _replacedStatusInterval = TimeSpan.FromMilliseconds(250);
    private static readonly TimeSpan _addedStatusInterval = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan _saveInterval = TimeSpan.FromSeconds(10);

    private readonly ISeedSearchService _search;
    private readonly ISearchSessionService _sessions;
    private readonly IConditionValidator _validator;
    private readonly ISeedSequenceFactory _sequenceFactory;
    private readonly IVersionCatalog _versions;
    private readonly IFileStore _files;
    private readonly ICliConsole _console;
    private readonly TimeProvider _timeProvider;

    public SearchCommand(ISeedSearchService search, ISearchSessionService sessions, IConditionValidator validator, ISeedSequenceFactory sequenceFactory, IVersionCatalog versions, IFileStore files, ICliConsole console, TimeProvider timeProvider)
    {
        _search = search;
        _sessions = sessions;
        _validator = validator;
        _sequenceFactory = sequenceFactory;
        _versions = versions;
        _files = files;
        _console = console;
        _timeProvider = timeProvider;
    }

    public async Task<int> RunAsync(SearchRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var content = await _files.ReadAsync(request.SessionPath, cancellationToken);
        if (!TryOpen(request.SessionPath, content))
            return ExitCodes.Failure;

        var setup = _sessions.Setup;
        _console.WriteMessage(DescribeSetup(setup));
        if (!request.Reset && IsLastSearch(setup, SearchState.Finished))
            return await ShowFinishedAsync(request.OutputPath);

        if (FindProblems(setup) is { Count: > 0 } problems)
        {
            ReportProblems(request.SessionPath, problems);
            return ExitCodes.Failure;
        }

        return await SearchAsync(request, setup, cancellationToken);
    }

    private bool TryOpen(string path, byte[] content)
    {
        try
        {
            _sessions.Import(content);
            return true;
        }
        catch (FormatException ex)
        {
            _console.WriteMessage($"{path}: {ex.Message}");
            return false;
        }
    }

    private bool IsLastSearch(SearchSetup setup, SearchState state) => _search.State == state && _search.Setup == setup;

    private async Task<int> ShowFinishedAsync(string? outputPath)
    {
        await using var file = OpenOutput(outputPath);
        new SeedWriter(file ?? _console.Output).WriteNew(_search.Results);
        _console.WriteMessage($"{DescribeOutcome()} Run with {CliOptionNames.Reset} to search again.");
        return ExitCodes.Success;
    }

    /// <summary>What keeps the app's Search button from starting a setup, and what the search itself refuses.</summary>
    private List<string> FindProblems(SearchSetup setup)
    {
        var definition = setup.Definition;
        if (definition.Conditions.Count == 0)
            return ["It has no conditions."];

        List<string> problems = [.. _validator.Validate(definition).Select(problem => problem.ToString()), .. _sequenceFactory.Validate(setup.Source, definition.Version)];
        if (setup.MaxResults is < 1 or > SearchSetup.MaxResultsLimit)
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"It stops after {setup.MaxResults} seeds instead of 1 to {SearchSetup.MaxResultsLimit:N0}."));
        if (setup.Workers < 0)
            problems.Add("It asks for a negative number of workers.");
        return problems;
    }

    private void ReportProblems(string path, List<string> problems)
    {
        _console.WriteMessage($"The search in {path} cannot run:");
        foreach (var problem in problems)
            _console.WriteMessage($"  {problem}");
    }

    private async Task<int> SearchAsync(SearchRequest request, SearchSetup setup, CancellationToken cancellationToken)
    {
        var resume = !request.Reset && IsLastSearch(setup, SearchState.Paused);
        await using var file = OpenOutput(request.OutputPath);
        var run = new SearchRun(new SeedWriter(file ?? _console.Output), request.SessionPath, GetStatusInterval(), _timeProvider.GetTimestamp());
        if (resume)
            ShowContinuation(run);
        else if (!request.Reset && _search.State != SearchState.Idle)
            _console.WriteMessage(SetupChangedMessage);

        EventHandler follow = (_, _) => Follow(run);
        _search.Changed += follow;
        try
        {
            await (resume ? _search.ResumeAsync(cancellationToken) : _search.StartAsync(setup, cancellationToken));
        }
        finally
        {
            _search.Changed -= follow;
            _console.ClearStatus();
        }

        run.Seeds.WriteNew(_search.Results);
        var saved = TrySave(request.SessionPath, out var error);
        if (!saved)
            _console.WriteMessage($"The progress could not be saved to {request.SessionPath}: {error}");

        _console.WriteMessage(DescribeOutcome());
        if (!saved)
            return ExitCodes.Failure;
        return _search.State == SearchState.Finished ? ExitCodes.Success : ExitCodes.Stopped;
    }

    private TextWriter? OpenOutput(string? path) => path is null ? null : _files.CreateText(path);

    private TimeSpan GetStatusInterval() => _console.CanReplaceStatus ? _replacedStatusInterval : _addedStatusInterval;

    private void ShowContinuation(SearchRun run)
    {
        run.Seeds.WriteNew(_search.Results);
        var found = SearchProgressText.Count(_search.Results.Count, "seed");
        _console.WriteMessage(_search.Progress.NextSeed is { } next
            ? string.Create(CultureInfo.InvariantCulture, $"Continuing before seed {next} with {found} found.")
            : $"Continuing with {found} found.");
    }

    /// <summary>
    /// Runs within the search, between two of its steps, which makes it safe to read the seeds it found; the reports of a
    /// preparation come from elsewhere but one at a time, and change no seeds.
    /// </summary>
    private void Follow(SearchRun run)
    {
        if (_search.State != SearchState.Running)
            return;

        run.Seeds.WriteNew(_search.Results);
        if (IsDue(run.LastStatus, run.StatusInterval))
        {
            _console.WriteStatus(DescribeStatus());
            run.LastStatus = _timeProvider.GetTimestamp();
        }

        if (IsDue(run.LastSave, _saveInterval))
            SaveWhileRunning(run);
    }

    private bool IsDue(long since, TimeSpan interval) => _timeProvider.GetElapsedTime(since) >= interval;

    private void SaveWhileRunning(SearchRun run)
    {
        var saved = TrySave(run.SessionPath, out var error);
        if (!saved && !run.IsSaveFailing)
            _console.WriteMessage($"The progress could not be saved to {run.SessionPath}, trying again later: {error}");

        run.IsSaveFailing = !saved;
        run.LastSave = _timeProvider.GetTimestamp();
    }

    private bool TrySave(string path, [NotNullWhen(false)] out string? error)
    {
        try
        {
            _files.Replace(path, _sessions.Export());
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    private string DescribeSetup(SearchSetup setup)
    {
        var definition = setup.Definition;
        var conditions = SearchProgressText.Count(definition.Conditions.Count, "condition");
        var largeBiomes = definition.LargeBiomes ? " with large biomes" : string.Empty;
        return $"Searching for {conditions} in Minecraft {_versions.GetName(definition.Version)}{largeBiomes}, stopping after {SearchProgressText.Count(setup.MaxResults, "seed")}.";
    }

    private string DescribeStatus()
    {
        var progress = SearchProgressText.DescribeProgress(_search.State, _search.Progress);
        return _search.Progress.Preparing is null ? $"{SearchProgressText.Count(_search.Results.Count, "seed")} found · {progress}" : progress;
    }

    private string DescribeOutcome()
    {
        var found = SearchProgressText.Count(_search.Results.Count, "seed");
        var progress = _search.Progress;
        return _search.State switch
        {
            SearchState.Finished when _search.Results.Count >= _sessions.Setup.MaxResults => string.Create(CultureInfo.InvariantCulture, $"Finished: found {found} after scanning {progress.Scanned:N0}."),
            SearchState.Finished => $"Finished: every seed was scanned, {found} found.",
            SearchState.Paused when progress.NextSeed is { } next => string.Create(CultureInfo.InvariantCulture, $"Stopped before seed {next} with {found} found. {ContinueHint}"),
            SearchState.Paused => $"Stopped with {found} found. {ContinueHint}",
            _ => throw new NotSupportedException($"Unsupported search state: {_search.State}"),
        };
    }

    /// <summary>Writes each seed a search finds once, in the order it found them.</summary>
    private sealed class SeedWriter(TextWriter writer)
    {
        private int _written;

        public void WriteNew(IReadOnlyList<long> seeds)
        {
            if (seeds.Count <= _written)
                return;

            for (var index = _written; index < seeds.Count; index++)
                writer.WriteLine(seeds[index].ToString(CultureInfo.InvariantCulture));
            writer.Flush();
            _written = seeds.Count;
        }
    }

    /// <summary>What one run of the command keeps track of while the search runs.</summary>
    private sealed class SearchRun(SeedWriter seeds, string sessionPath, TimeSpan statusInterval, long startTimestamp)
    {
        public SeedWriter Seeds { get; } = seeds;
        public string SessionPath { get; } = sessionPath;
        public TimeSpan StatusInterval { get; } = statusInterval;
        public long LastStatus { get; set; } = startTimestamp;
        public long LastSave { get; set; } = startTimestamp;
        public bool IsSaveFailing { get; set; }
    }
}

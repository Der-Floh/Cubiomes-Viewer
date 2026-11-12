using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.ConditionServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Service.SearchServ;

/// <summary>
/// Hands runs of consecutive seeds to the workers and takes their results in the order of the seeds, so that the
/// search finds the same seeds however many workers it has and can continue from one position.
/// </summary>
public sealed class SeedSearchService : ISeedSearchService
{
    private const int FirstBatchSeeds = 64;
    private const int MinBatchSeeds = 16;
    private const int MaxBatchSeeds = 1 << 20;
    private const int MaxBatchGrowth = 4;
    private const double BatchMilliseconds = 150;
    private static readonly TimeSpan _speedWindow = TimeSpan.FromSeconds(5);
    private static readonly TimeSpan _progressInterval = TimeSpan.FromMilliseconds(100);
    private static readonly JobPriority _priority = new(JobPriorityClass.Background);

    public SearchState State { get; private set; }
    public SearchSetup? Setup { get; private set; }
    public SearchProgress Progress { get; private set; }
    public IReadOnlyList<long> Results => _results;
    public int MaxWorkers => _compute.WorkerCount;
    public int AutomaticWorkers => Math.Max(1, _compute.WorkerCount - 1);

    public event EventHandler? Changed;

    private readonly IComputeService _compute;
    private readonly IConditionValidator _validator;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SeedSearchService> _logger;
    private readonly List<long> _results = [];
    private readonly Dictionary<UInt128, SearchBatchResult> _completed = [];
    private readonly Queue<(long Timestamp, UInt128 Scanned)> _speedSamples = [];

    /// <summary>Seeds counted from the first seed of the search: how many are accepted in order, and how many are handed out.</summary>
    private UInt128 _scanned;
    private UInt128 _scheduled;
    private UInt128 _total;
    private int _batchSeeds = FirstBatchSeeds;
    private long _lastProgressTimestamp;

    public SeedSearchService(IComputeService compute, IConditionValidator validator, TimeProvider timeProvider, ILogger<SeedSearchService> logger)
    {
        _compute = compute;
        _validator = validator;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task StartAsync(SearchSetup setup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ThrowIfRunning();
        Validate(setup);

        Adopt(setup, scanned: 0, results: []);
        return RunAsync(setup, cancellationToken);
    }

    public Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        if (State != SearchState.Paused || Setup is not { } setup)
            throw new InvalidOperationException("No search is paused.");

        return RunAsync(setup, cancellationToken);
    }

    public SearchSnapshot? CreateSnapshot()
    {
        if (Setup is not { } setup)
            return null;

        return new SearchSnapshot
        {
            Setup = setup,
            NextSeed = Progress.NextSeed,
            Results = [.. _results],
            IsFinished = State == SearchState.Finished,
        };
    }

    public void Restore(SearchSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ThrowIfRunning();
        Validate(snapshot.Setup);

        var setup = snapshot.Setup;
        var total = CountSeeds(setup);
        var scanned = snapshot.IsFinished && snapshot.Results.Count < setup.MaxResults ? total : UInt128.Min(GetOffset(setup, snapshot.NextSeed), total);
        Adopt(setup, scanned, snapshot.Results);
        SetState(snapshot.IsFinished || scanned == total || _results.Count >= setup.MaxResults ? SearchState.Finished : SearchState.Paused);
    }

    public void Reset()
    {
        ThrowIfRunning();

        Setup = null;
        _results.Clear();
        _scanned = _scheduled = _total = 0;
        Progress = default;
        SetState(SearchState.Idle);
    }

    private async Task RunAsync(SearchSetup setup, CancellationToken cancellationToken)
    {
        var workers = setup.Workers > 0 ? Math.Min(setup.Workers, MaxWorkers) : AutomaticWorkers;
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        List<Task<SearchBatchResult>> running = [];
        var finished = false;

        _scheduled = _scanned;
        _speedSamples.Clear();
        _logger.LogInformation("Searching from seed {NextSeed} with {Workers} worker(s)", Progress.NextSeed, workers);
        SetState(SearchState.Running);

        try
        {
            while (!IsComplete(setup))
            {
                while (running.Count < workers && TryTakeBatch(setup, out var job))
                    running.Add(_compute.RunAsync(job, _priority, stopping.Token));
                if (running.Count == 0)
                    break;

                var batch = await Task.WhenAny(running);
                running.Remove(batch);
                Accept(setup, await batch);
            }

            finished = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("The search was stopped at seed {NextSeed}", Progress.NextSeed);
        }
        finally
        {
            await stopping.CancelAsync();
            Forget(running);
            _completed.Clear();
            UpdateProgress(setup);
            SetState(finished ? SearchState.Finished : SearchState.Paused);
        }
    }

    private bool IsComplete(SearchSetup setup) => _scanned >= _total || _results.Count >= setup.MaxResults;

    private bool TryTakeBatch(SearchSetup setup, [NotNullWhen(true)] out SearchBatchJob? job)
    {
        job = null;
        if (_scheduled >= _total)
            return false;

        var count = (int)UInt128.Min((UInt128)_batchSeeds, _total - _scheduled);
        job = new SearchBatchJob(setup.Definition, GetSeed(setup, _scheduled), count);
        _scheduled += (UInt128)count;
        return true;
    }

    private void Accept(SearchSetup setup, SearchBatchResult batch)
    {
        AdaptBatchSize(batch);
        _completed.Add(GetOffset(setup, batch.FirstSeed), batch);

        var found = false;
        while (!IsComplete(setup) && _completed.Remove(_scanned, out var next))
            found |= Append(setup, next);

        RecordSpeed();
        if (found || IsComplete(setup) || _timeProvider.GetElapsedTime(_lastProgressTimestamp) >= _progressInterval)
        {
            UpdateProgress(setup);
            RaiseChanged();
        }
    }

    /// <summary>Takes the seeds a batch found, and ends the search right behind the one that makes them enough.</summary>
    private bool Append(SearchSetup setup, SearchBatchResult batch)
    {
        foreach (var seed in batch.Matches)
        {
            _results.Add(seed);
            if (_results.Count < setup.MaxResults)
                continue;

            _scanned = GetOffset(setup, seed) + 1;
            return true;
        }

        _scanned += (UInt128)batch.Count;
        return batch.Matches.Length > 0;
    }

    private void AdaptBatchSize(SearchBatchResult batch)
    {
        if (batch.ElapsedMilliseconds <= 0 || batch.Count == 0)
        {
            _batchSeeds = Math.Min(_batchSeeds * MaxBatchGrowth, MaxBatchSeeds);
            return;
        }

        var wanted = batch.Count * BatchMilliseconds / batch.ElapsedMilliseconds;
        _batchSeeds = (int)Math.Clamp(wanted, MinBatchSeeds, Math.Min((double)_batchSeeds * MaxBatchGrowth, MaxBatchSeeds));
    }

    private void RecordSpeed()
    {
        var now = _timeProvider.GetTimestamp();
        _speedSamples.Enqueue((now, _scanned));
        while (_speedSamples.Count > 2 && _timeProvider.GetElapsedTime(_speedSamples.Peek().Timestamp, now) > _speedWindow)
            _speedSamples.Dequeue();
    }

    private void UpdateProgress(SearchSetup setup)
    {
        var speed = GetSpeed();
        var nextSeed = _scanned < _total ? GetSeed(setup, _scanned) : setup.LastSeed;
        Progress = new SearchProgress((long)UInt128.Min(_scanned, (UInt128)long.MaxValue), nextSeed, speed, GetRemaining(setup, speed));
        _lastProgressTimestamp = _timeProvider.GetTimestamp();
    }

    private double GetSpeed()
    {
        if (_speedSamples.Count < 2)
            return 0;

        var (firstTimestamp, firstScanned) = _speedSamples.Peek();
        var seconds = _timeProvider.GetElapsedTime(firstTimestamp, _timeProvider.GetTimestamp()).TotalSeconds;
        return seconds > 0 ? (double)(_scanned - firstScanned) / seconds : 0;
    }

    private TimeSpan? GetRemaining(SearchSetup setup, double seedsPerSecond)
    {
        if (setup.LastSeed == long.MaxValue || seedsPerSecond <= 0 || _scanned >= _total)
            return null;

        var seconds = (double)(_total - _scanned) / seedsPerSecond;
        return seconds < TimeSpan.MaxValue.TotalSeconds ? TimeSpan.FromSeconds(seconds) : null;
    }

    private void Adopt(SearchSetup setup, UInt128 scanned, IReadOnlyList<long> results)
    {
        Setup = setup;
        _results.Clear();
        _results.AddRange(results.Take(setup.MaxResults));
        _completed.Clear();
        _speedSamples.Clear();
        _total = CountSeeds(setup);
        _scanned = _scheduled = scanned;
        _batchSeeds = FirstBatchSeeds;
        UpdateProgress(setup);
    }

    private void Validate(SearchSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup.Definition);
        ArgumentOutOfRangeException.ThrowIfLessThan(setup.LastSeed, setup.FirstSeed);
        ArgumentOutOfRangeException.ThrowIfLessThan(setup.MaxResults, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(setup.MaxResults, SearchSetup.MaxResultsLimit);
        ArgumentOutOfRangeException.ThrowIfNegative(setup.Workers);

        var problems = _validator.Validate(setup.Definition);
        if (problems.Count > 0)
            throw new InvalidSearchException(problems);
    }

    private void ThrowIfRunning()
    {
        if (State == SearchState.Running)
            throw new InvalidOperationException("A search is running.");
    }

    private void SetState(SearchState state)
    {
        State = state;
        RaiseChanged();
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);

    /// <summary>Batches nobody waits for any more must not surface as unobserved failures.</summary>
    private static void Forget(List<Task<SearchBatchResult>> batches)
    {
        foreach (var batch in batches)
            _ = batch.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }

    private static UInt128 CountSeeds(SearchSetup setup) => (UInt128)GetDistance(setup.FirstSeed, setup.LastSeed) + 1;

    private static UInt128 GetOffset(SearchSetup setup, long seed) => seed < setup.FirstSeed ? 0 : GetDistance(setup.FirstSeed, seed);

    private static ulong GetDistance(long from, long to) => unchecked((ulong)to - (ulong)from);

    private static long GetSeed(SearchSetup setup, UInt128 offset) => unchecked(setup.FirstSeed + (long)(ulong)offset);
}

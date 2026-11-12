using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.ConditionServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Service.SearchServ;

/// <summary>
/// Hands batches of the source's seeds to the workers and takes their results in the order of the source, so that the
/// search finds the same seeds however many workers it has and can continue from one position.
/// </summary>
public sealed class SeedSearchService : ISeedSearchService
{
    private const int FirstBatchSeeds = 64;
    private const int MinBatchSeeds = 16;
    private const int MaxBatchGrowth = 4;
    private const double BatchMilliseconds = 150;

    /// <summary>How long a worker may take on a batch before it hands back the rest; most batches take far less.</summary>
    private const int MaxBatchMilliseconds = 600;
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
    private readonly ISeedSequenceFactory _sequenceFactory;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<SeedSearchService> _logger;
    private readonly List<long> _results = [];
    private readonly Dictionary<UInt128, SearchBatchResult> _completed = [];
    private readonly Queue<(long Timestamp, UInt128 Scanned)> _speedSamples = [];

    /// <summary>The rest of batches a worker stopped early, by where they start; they go out before any new seeds.</summary>
    private readonly PriorityQueue<(UInt128 Position, UInt128 End), UInt128> _unfinished = new();

    /// <summary>The seeds of the source; prepared when a search runs, since some sources take a while to prepare.</summary>
    private ISeedSequence? _sequence;

    /// <summary>Positions in the source: how many seeds are accepted in order, and how many are handed out.</summary>
    private UInt128 _scanned;
    private UInt128 _scheduled;
    private int _batchSeeds = FirstBatchSeeds;
    private long _lastProgressTimestamp;

    public SeedSearchService(IComputeService compute, IConditionValidator validator, ISeedSequenceFactory sequenceFactory, TimeProvider timeProvider, ILogger<SeedSearchService> logger)
    {
        _compute = compute;
        _validator = validator;
        _sequenceFactory = sequenceFactory;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public Task StartAsync(SearchSetup setup, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(setup);
        ThrowIfRunning();
        Validate(setup);

        Adopt(setup, scanned: 0, nextSeed: null, results: []);
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
            Scanned = Progress.Scanned,
            NextSeed = Progress.NextSeed,
            Results = [.. _results],
            IsFinished = State == SearchState.Finished,
        };
    }

    public void Restore(SearchSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentOutOfRangeException.ThrowIfNegative(snapshot.Scanned);
        ThrowIfRunning();
        Validate(snapshot.Setup);

        Adopt(snapshot.Setup, (UInt128)snapshot.Scanned, snapshot.NextSeed, snapshot.Results);
        SetState(snapshot.IsFinished || _results.Count >= snapshot.Setup.MaxResults ? SearchState.Finished : SearchState.Paused);
    }

    public void Reset()
    {
        ThrowIfRunning();

        Setup = null;
        _sequence = null;
        _results.Clear();
        _scanned = _scheduled = 0;
        Progress = default;
        SetState(SearchState.Idle);
    }

    private async Task RunAsync(SearchSetup setup, CancellationToken cancellationToken)
    {
        var workers = setup.Workers > 0 ? Math.Min(setup.Workers, MaxWorkers) : AutomaticWorkers;
        using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        List<(Task<SearchBatchResult> Batch, UInt128 Position, SearchJob Job)> running = [];
        var finished = false;

        _speedSamples.Clear();
        SetState(SearchState.Running);

        try
        {
            var sequence = _sequence ??= await PrepareAsync(setup, cancellationToken);
            _scanned = _scheduled = UInt128.Min(_scanned, sequence.Count);
            _logger.LogInformation("Searching from position {Scanned} of {Count} with {Workers} worker(s)", _scanned, sequence.Count, workers);

            while (!IsComplete(setup, sequence))
            {
                while (running.Count < workers && TryTakeBatch(setup, sequence, out var job, out var position))
                    running.Add((_compute.RunAsync(job, _priority, stopping.Token), position, job));
                if (running.Count == 0)
                    break;

                var batch = await Task.WhenAny(running.Select(entry => entry.Batch));
                var done = running.Single(entry => entry.Batch == batch);
                running.Remove(done);
                Accept(setup, sequence, done.Position, done.Job, await batch);
            }

            finished = true;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            _logger.LogInformation("The search was stopped at position {Scanned}", _scanned);
        }
        finally
        {
            await stopping.CancelAsync();
            Forget(running.Select(entry => entry.Batch));
            _completed.Clear();
            _unfinished.Clear();
            UpdateProgress(setup);
            SetState(finished ? SearchState.Finished : SearchState.Paused);
        }
    }

    /// <summary>Prepares the seeds of the source and reports how far that has come while it takes.</summary>
    private async Task<ISeedSequence> PrepareAsync(SearchSetup setup, CancellationToken cancellationToken)
    {
        var progress = new PreparationProgress(this);
        try
        {
            return await _sequenceFactory.CreateAsync(setup.Source, setup.Definition.Version, progress, cancellationToken);
        }
        finally
        {
            progress.Complete();
            Progress = Progress with { Preparing = null };
        }
    }

    private void ShowPreparation(double share)
    {
        Progress = Progress with { Preparing = share };
        RaiseChanged();
    }

    private bool IsComplete(SearchSetup setup, ISeedSequence sequence) => _scanned >= sequence.Count || _results.Count >= setup.MaxResults;

    private bool TryTakeBatch(SearchSetup setup, ISeedSequence sequence, [NotNullWhen(true)] out SearchJob? job, out UInt128 position)
        => TryTakeUnfinished(setup, sequence, out job, out position) || TryTakeNext(setup, sequence, out job, out position);

    private bool TryTakeUnfinished(SearchSetup setup, ISeedSequence sequence, [NotNullWhen(true)] out SearchJob? job, out UInt128 position)
    {
        job = null;
        position = default;
        if (!_unfinished.TryDequeue(out var rest, out _))
            return false;

        position = rest.Position;
        job = CreateBatch(setup, sequence, position, (int)UInt128.Min(rest.End - position, (UInt128)_batchSeeds));
        KeepUnfinished(position + (UInt128)job.Count, rest.End);
        return true;
    }

    private bool TryTakeNext(SearchSetup setup, ISeedSequence sequence, [NotNullWhen(true)] out SearchJob? job, out UInt128 position)
    {
        job = null;
        position = _scheduled;
        if (_scheduled >= sequence.Count)
            return false;

        job = CreateBatch(setup, sequence, _scheduled, _batchSeeds);
        _scheduled += (UInt128)job.Count;
        return true;
    }

    /// <summary>A batch that needs no more matches than the search still lacks, since the results are taken in order.</summary>
    private SearchJob CreateBatch(SearchSetup setup, ISeedSequence sequence, UInt128 position, int maxCount)
        => sequence.CreateBatch(setup.Definition, position, maxCount) with { MaxMatches = setup.MaxResults - _results.Count, MaxMilliseconds = MaxBatchMilliseconds };

    private void KeepUnfinished(UInt128 position, UInt128 end)
    {
        if (position < end)
            _unfinished.Enqueue((position, end), position);
    }

    private void Accept(SearchSetup setup, ISeedSequence sequence, UInt128 position, SearchJob job, SearchBatchResult batch)
    {
        if (batch.Count < 1 || batch.Count > job.Count)
            throw new InvalidOperationException($"A worker tested {batch.Count} of the {job.Count} seeds of a batch.");

        KeepUnfinished(position + (UInt128)batch.Count, position + (UInt128)job.Count);
        AdaptBatchSize(batch, sequence.MaxBatchCount);
        _completed.Add(position, batch);

        var found = false;
        while (!IsComplete(setup, sequence) && _completed.Remove(_scanned, out var next))
            found |= Append(setup, sequence, next);

        RecordSpeed();
        if (found || IsComplete(setup, sequence) || _timeProvider.GetElapsedTime(_lastProgressTimestamp) >= _progressInterval)
        {
            UpdateProgress(setup);
            RaiseChanged();
        }
    }

    /// <summary>Takes the seeds a batch found, and ends the search right behind the one that makes them enough.</summary>
    private bool Append(SearchSetup setup, ISeedSequence sequence, SearchBatchResult batch)
    {
        foreach (var index in batch.Matches)
        {
            _results.Add(sequence.GetSeed(_scanned + (UInt128)index));
            if (_results.Count < setup.MaxResults)
                continue;

            _scanned += (UInt128)index + 1;
            return true;
        }

        _scanned += (UInt128)batch.Count;
        return batch.Matches.Length > 0;
    }

    private void AdaptBatchSize(SearchBatchResult batch, int maxBatchSeeds)
    {
        var grown = Math.Min((double)_batchSeeds * MaxBatchGrowth, maxBatchSeeds);
        var wanted = batch.ElapsedMilliseconds > 0 ? batch.Count * BatchMilliseconds / batch.ElapsedMilliseconds : grown;
        _batchSeeds = (int)Math.Clamp(wanted, MinBatchSeeds, grown);
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
        var nextSeed = _sequence is { } sequence ? GetNextSeed(setup, sequence) : Progress.NextSeed;
        Progress = new SearchProgress((long)UInt128.Min(_scanned, (UInt128)long.MaxValue), nextSeed, speed, GetRemaining(speed), Progress.Preparing);
        _lastProgressTimestamp = _timeProvider.GetTimestamp();
    }

    private long? GetNextSeed(SearchSetup setup, ISeedSequence sequence)
        => IsComplete(setup, sequence) ? null : sequence.GetSeed(_scanned);

    private double GetSpeed()
    {
        if (_speedSamples.Count < 2)
            return 0;

        var (firstTimestamp, firstScanned) = _speedSamples.Peek();
        var seconds = _timeProvider.GetElapsedTime(firstTimestamp, _timeProvider.GetTimestamp()).TotalSeconds;
        return seconds > 0 ? (double)(_scanned - firstScanned) / seconds : 0;
    }

    private TimeSpan? GetRemaining(double seedsPerSecond)
    {
        if (_sequence is not { IsEndless: false } sequence || seedsPerSecond <= 0 || _scanned >= sequence.Count)
            return null;

        var seconds = (double)(sequence.Count - _scanned) / seedsPerSecond;
        return seconds < TimeSpan.MaxValue.TotalSeconds ? TimeSpan.FromSeconds(seconds) : null;
    }

    private void Adopt(SearchSetup setup, UInt128 scanned, long? nextSeed, IReadOnlyList<long> results)
    {
        Setup = setup;
        _sequence = null;
        _results.Clear();
        _results.AddRange(results.Take(setup.MaxResults));
        _completed.Clear();
        _unfinished.Clear();
        _speedSamples.Clear();
        _scanned = _scheduled = scanned;
        _batchSeeds = FirstBatchSeeds;
        Progress = new SearchProgress((long)UInt128.Min(scanned, (UInt128)long.MaxValue), nextSeed, 0, null);
    }

    private void Validate(SearchSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup.Definition);
        ArgumentNullException.ThrowIfNull(setup.Source);
        ArgumentOutOfRangeException.ThrowIfLessThan(setup.MaxResults, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(setup.MaxResults, SearchSetup.MaxResultsLimit);
        ArgumentOutOfRangeException.ThrowIfNegative(setup.Workers);

        List<ConditionProblem> problems = [.. _validator.Validate(setup.Definition)];
        problems.AddRange(_sequenceFactory.Validate(setup.Source, setup.Definition.Version).Select(message => new ConditionProblem(0, message)));
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

    /// <summary>
    /// Shows the reports of a preparation on the context the search was started from, one after the other even where
    /// there is none, and none once the preparation is over.
    /// </summary>
    private sealed class PreparationProgress(SeedSearchService search) : IProgress<double>
    {
        private readonly SynchronizationContext? _context = SynchronizationContext.Current;
        private readonly object _gate = new();
        private bool _isComplete;

        public void Report(double value)
        {
            if (_context is null)
                Show(value);
            else
                _context.Post(_ => Show(value), null);
        }

        public void Complete()
        {
            lock (_gate)
                _isComplete = true;
        }

        private void Show(double share)
        {
            lock (_gate)
            {
                if (!_isComplete)
                    search.ShowPreparation(share);
            }
        }
    }

    /// <summary>Batches nobody waits for any more must not surface as unobserved failures.</summary>
    private static void Forget(IEnumerable<Task<SearchBatchResult>> batches)
    {
        foreach (var batch in batches)
            _ = batch.ContinueWith(static task => _ = task.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}

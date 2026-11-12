using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

using Microsoft.Extensions.Logging.Abstractions;

namespace Cubiomes_Viewer.Engine.Tests.Service.SearchServ;

/// <summary>
/// How the search hands out batches and puts their results together, on one thread and with workers that follow a
/// script, so that every run takes the same course.
/// </summary>
public sealed class SeedSearchSchedulingTests
{
    private const int Workers = 4;
    private const int MatchEvery = 1000;
    private const double SeedsPerMillisecond = 20;

    private static readonly SearchDefinition _definition = ConditionTestBench.Define(McVersion.V1_16_5, new SpawnCondition { Id = 1 });

    private readonly ManualTimeProvider _time = new();
    private readonly ScriptedComputeService _compute;
    private readonly SeedSearchService _search;

    public SeedSearchSchedulingTests()
    {
        _compute = new ScriptedComputeService(Workers, _time);
        _search = new SeedSearchService(_compute, TestEngine.CreateConditionValidator(), TestEngine.CreateSeedSequenceFactory(), _time, NullLogger<SeedSearchService>.Instance);
    }

    [Fact]
    public void StartAsync_BatchesFinishOutOfOrder_KeepsTheSeedsInOrder() => SingleThread.Run(async () =>
    {
        var setup = new SearchSetup { Definition = _definition, Source = new SeedRangeSource { FirstSeed = -5000, LastSeed = 250_000 }, MaxResults = 10_000, Workers = Workers };

        await _search.StartAsync(setup, TestContext.Current.CancellationToken);

        Assert.Equal(Enumerable.Range(-5, 256).Select(index => index * (long)MatchEvery), _search.Results);
        Assert.Equal(255_001, _search.Progress.Scanned);
        Assert.Equal(SearchState.Finished, _search.State);
        Assert.True(_compute.AnsweredOutOfOrder > 0);
    });

    [Fact]
    public void StartAsync_EnoughSeedsFound_DropsWhatLaterBatchesFound() => SingleThread.Run(async () =>
    {
        var setup = new SearchSetup { Definition = _definition, Source = new SeedRangeSource { FirstSeed = 1 }, MaxResults = 12, Workers = Workers };

        await _search.StartAsync(setup, TestContext.Current.CancellationToken);

        Assert.Equal(Enumerable.Range(1, 12).Select(index => index * (long)MatchEvery), _search.Results);
        Assert.Null(_search.Progress.NextSeed);
        Assert.Equal(12 * MatchEvery, _search.Progress.Scanned);
        Assert.Equal(SearchState.Finished, _search.State);
    });

    [Theory]
    [InlineData(0, Workers - 1)]
    [InlineData(2, 2)]
    [InlineData(99, Workers)]
    public void StartAsync_Workers_RunsThatManyBatchesAtOnce(int asked, int expected) => SingleThread.Run(async () =>
    {
        var setup = new SearchSetup { Definition = _definition, Source = new SeedRangeSource { LastSeed = 100_000 }, Workers = asked };

        await _search.StartAsync(setup, TestContext.Current.CancellationToken);

        Assert.Equal(expected, _compute.MostBatchesAtOnce);
    });

    [Fact]
    public void StartAsync_Batches_GrowUntilTheyTakeAFractionOfASecond() => SingleThread.Run(async () =>
    {
        var setup = new SearchSetup { Definition = _definition, Source = new SeedRangeSource { LastSeed = 1_000_000 }, MaxResults = 10_000, Workers = 1 };

        await _search.StartAsync(setup, TestContext.Current.CancellationToken);

        Assert.Equal([64, 256, 1024, 3000], _compute.BatchSizes.Take(4));
        Assert.Equal(3000, _compute.BatchSizes.Max());
        Assert.Equal(1_000_001, _compute.BatchSizes.Sum());
    });

    [Fact]
    public void StartAsync_SearchWithAnEnd_ReportsItsSpeedAndTheTimeLeft() => SingleThread.Run(async () =>
    {
        var setup = new SearchSetup { Definition = _definition, Source = new SeedRangeSource { LastSeed = 999_999 }, MaxResults = 10_000, Workers = 1 };
        List<SearchProgress> reports = [];
        _search.Changed += (_, _) => reports.Add(_search.Progress);

        await _search.StartAsync(setup, TestContext.Current.CancellationToken);

        var halfway = reports.First(progress => progress.Scanned >= 500_000);
        var remaining = Assert.NotNull(halfway.Remaining);
        Assert.Equal(SeedsPerMillisecond * 1000, halfway.SeedsPerSecond, tolerance: 1);
        Assert.Equal((1_000_000 - halfway.Scanned) / (SeedsPerMillisecond * 1000), remaining.TotalSeconds, tolerance: 0.01);
        Assert.Null(reports[^1].Remaining);
        Assert.All(reports.Zip(reports.Skip(1)), pair => Assert.True(pair.First.Scanned <= pair.Second.Scanned));
    });

    [Fact]
    public void StartAsync_SearchWithoutAnEnd_HasNoTimeLeftToReport() => SingleThread.Run(async () =>
    {
        var setup = new SearchSetup { Definition = _definition, MaxResults = 50, Workers = 1 };
        List<SearchProgress> reports = [];
        _search.Changed += (_, _) => reports.Add(_search.Progress);

        await _search.StartAsync(setup, TestContext.Current.CancellationToken);

        Assert.Contains(reports, progress => progress.SeedsPerSecond > 0);
        Assert.All(reports, progress => Assert.Null(progress.Remaining));
    });

    [Fact]
    public void StartAsync_BatchFails_ThrowsAndCanContinueWhereItStopped() => SingleThread.Run(async () =>
    {
        var setup = new SearchSetup { Definition = _definition, Source = new SeedRangeSource { LastSeed = 99_999 }, MaxResults = 10_000, Workers = 1 };
        _compute.FailAtSeed = 40_000;

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _search.StartAsync(setup, TestContext.Current.CancellationToken));
        var paused = (_search.State, NextSeed: _search.Progress.NextSeed ?? -1);
        _compute.FailAtSeed = null;
        await _search.ResumeAsync(TestContext.Current.CancellationToken);

        Assert.Equal("The worker broke.", exception.Message);
        Assert.Equal(SearchState.Paused, paused.State);
        Assert.InRange(paused.NextSeed, 1, 40_000);
        Assert.Equal(Enumerable.Range(0, 100).Select(index => index * (long)MatchEvery), _search.Results);
        Assert.Equal(SearchState.Finished, _search.State);
    });

    [Fact]
    public void StartAsync_RangeUpToTheLastSeedThereIs_EndsThere() => SingleThread.Run(async () =>
    {
        var setup = new SearchSetup { Definition = _definition, Source = new SeedRangeSource { FirstSeed = long.MaxValue - 2500 }, MaxResults = 10_000, Workers = Workers };

        await _search.StartAsync(setup, TestContext.Current.CancellationToken);

        Assert.Equal(2501, _search.Progress.Scanned);
        Assert.Null(_search.Progress.NextSeed);
        Assert.Equal(SearchState.Finished, _search.State);
        Assert.Equal([9223372036854774000, 9223372036854775000], _search.Results);
    });

    /// <summary>
    /// Workers that find every thousandth seed and take a fixed time per seed. Whenever the search waits they answer
    /// one of the batches they hold: the newest and the oldest in turns.
    /// </summary>
    private sealed class ScriptedComputeService(int workers, ManualTimeProvider time) : IComputeService
    {
        public int WorkerCount => workers;
        public int MostBatchesAtOnce { get; private set; }
        public int AnsweredOutOfOrder { get; private set; }
        public List<int> BatchSizes { get; } = [];
        public long? FailAtSeed { get; set; }

        private readonly List<(SearchBatchJob Job, TaskCompletionSource<SearchBatchResult> Completion)> _held = [];
        private int _answers;

        public Task<TResult> RunAsync<TResult>(ComputeJob<TResult> job, JobPriority priority, CancellationToken cancellationToken = default)
        {
            var batch = Assert.IsType<SearchBatchJob>(job);
            var completion = new TaskCompletionSource<SearchBatchResult>();
            BatchSizes.Add(batch.Count);
            _held.Add((batch, completion));
            MostBatchesAtOnce = Math.Max(MostBatchesAtOnce, _held.Count);
            _ = AnswerLaterAsync();
            return (Task<TResult>)(object)completion.Task;
        }

        private async Task AnswerLaterAsync()
        {
            await Task.Yield();

            var index = _answers++ % 2 == 0 ? _held.Count - 1 : 0;
            var (job, completion) = _held[index];
            _held.RemoveAt(index);
            AnsweredOutOfOrder += index > 0 ? 1 : 0;
            Answer(job, completion);
        }

        private void Answer(SearchBatchJob job, TaskCompletionSource<SearchBatchResult> completion)
        {
            var milliseconds = job.Count / SeedsPerMillisecond;
            time.Advance(TimeSpan.FromMilliseconds(milliseconds));

            if (FailAtSeed is { } failing && failing >= job.FirstSeed && failing - job.FirstSeed < job.Count)
            {
                completion.SetException(new InvalidOperationException("The worker broke."));
                return;
            }

            int[] matches = [.. Enumerable.Range(0, job.Count).Where(index => job.GetSeed(index) % MatchEvery == 0)];
            completion.SetResult(new SearchBatchResult { Count = job.Count, Matches = matches, ElapsedMilliseconds = milliseconds });
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        public override long TimestampFrequency => TimeSpan.TicksPerSecond;

        private long _ticks;

        public override long GetTimestamp() => _ticks;

        public void Advance(TimeSpan time) => _ticks += time.Ticks;
    }
}

using System.Diagnostics;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.ConditionServ;

namespace Cubiomes_Viewer.Engine.Service.SearchServ;

/// <summary>
/// Tests the sister seeds of structure seeds: each structure seed once with the quick pass, which decides for all of its
/// sister seeds at once, and the sister seeds one by one only where the quick pass cannot decide.
/// </summary>
public sealed class StructureSeedBatchJobHandler : IJobHandler<StructureSeedBatchJob, SearchBatchResult>
{
    /// <summary>The most matches one batch returns, however many the search asks for; the rest of the batch goes into another one.</summary>
    public const int MaxMatches = SearchSetup.MaxResultsLimit;

    private readonly ISearchPlanCompiler _planCompiler;
    private readonly ISeedMatcher _seedMatcher;

    public StructureSeedBatchJobHandler(ISearchPlanCompiler planCompiler, ISeedMatcher seedMatcher)
    {
        _planCompiler = planCompiler;
        _seedMatcher = seedMatcher;
    }

    public SearchBatchResult Handle(StructureSeedBatchJob job, IWorkerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(job.Count);
        ArgumentOutOfRangeException.ThrowIfNegative(job.FirstUpperBits);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(job.FirstUpperBits, StructureSeed.SisterSeeds);

        var scan = new Scan(job, _planCompiler.Compile(job.Definition), new SearchSeedContext(context, job.Definition), _seedMatcher, cancellationToken);
        scan.Run();
        return scan.ToResult();
    }

    /// <summary>One pass over the seeds of a batch, which stops early when it runs out of time or has all the matches it may return.</summary>
    private sealed class Scan(StructureSeedBatchJob job, SearchPlan plan, SearchSeedContext seedContext, ISeedMatcher seedMatcher, CancellationToken cancellationToken)
    {
        private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
        private readonly List<int> _matches = [];
        private readonly int _maxMatches = Math.Min(job.MaxMatches, MaxMatches);
        private int _tested;

        public void Run()
        {
            while (_tested < job.Count && !MustStop())
            {
                var (structureSeed, upperBits) = job.Locate(_tested);
                TestSisterSeeds(structureSeed, upperBits, Math.Min(job.Count - _tested, StructureSeed.SisterSeeds - upperBits));
            }
        }

        public SearchBatchResult ToResult() => new()
        {
            Count = _tested,
            Matches = [.. _matches],
            ElapsedMilliseconds = _stopwatch.Elapsed.TotalMilliseconds,
        };

        private bool MustStop() => _tested > 0 && (_matches.Count >= _maxMatches || _stopwatch.ElapsedMilliseconds >= job.MaxMilliseconds);

        private void TestSisterSeeds(long structureSeed, int firstUpperBits, int count)
        {
            cancellationToken.ThrowIfCancellationRequested();

            seedContext.Open(structureSeed);
            switch (seedMatcher.Evaluate(plan, seedContext, SearchPass.Quick))
            {
                case SearchOutcome.Failed:
                    _tested += count;
                    break;
                case SearchOutcome.Passed:
                    TakeAll(count);
                    break;
                default:
                    TestOneByOne(structureSeed, firstUpperBits, count);
                    break;
            }
        }

        private void TakeAll(int count)
        {
            var taken = Math.Min(count, _maxMatches - _matches.Count);
            _matches.AddRange(Enumerable.Range(_tested, taken));
            _tested += taken;
        }

        private void TestOneByOne(long structureSeed, int firstUpperBits, int count)
        {
            for (var offset = 0; offset < count && (offset == 0 || !MustStop()); offset++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                seedContext.Open(StructureSeed.ToWorldSeed(structureSeed, firstUpperBits + offset));
                if (seedMatcher.Evaluate(plan, seedContext, SearchPass.Full) == SearchOutcome.Passed)
                    _matches.Add(_tested);
                _tested++;
            }
        }
    }
}

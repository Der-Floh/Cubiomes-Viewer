using System.Diagnostics;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.ConditionServ;

namespace Cubiomes_Viewer.Engine.Service.SearchServ;

/// <summary>Tests a run of consecutive seeds against a definition.</summary>
public sealed class SearchBatchJobHandler : IJobHandler<SearchBatchJob, SearchBatchResult>
{
    private readonly ISearchPlanCompiler _planCompiler;
    private readonly ISeedMatcher _seedMatcher;

    public SearchBatchJobHandler(ISearchPlanCompiler planCompiler, ISeedMatcher seedMatcher)
    {
        _planCompiler = planCompiler;
        _seedMatcher = seedMatcher;
    }

    public SearchBatchResult Handle(SearchBatchJob job, IWorkerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(context);
        ArgumentOutOfRangeException.ThrowIfNegative(job.Count);

        var stopwatch = Stopwatch.StartNew();
        var plan = _planCompiler.Compile(job.Definition);
        var seedContext = new SearchSeedContext(context, job.Definition);
        List<long> matches = [];

        for (var index = 0; index < job.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var seed = unchecked(job.FirstSeed + index);
            seedContext.Open(seed);
            if (_seedMatcher.IsMatch(plan, seedContext))
                matches.Add(seed);
        }

        return new SearchBatchResult
        {
            FirstSeed = job.FirstSeed,
            Count = job.Count,
            Matches = [.. matches],
            ElapsedMilliseconds = stopwatch.Elapsed.TotalMilliseconds,
        };
    }
}

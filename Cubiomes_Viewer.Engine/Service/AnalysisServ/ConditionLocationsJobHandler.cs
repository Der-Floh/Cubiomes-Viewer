using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.ConditionServ;

namespace Cubiomes_Viewer.Engine.Service.AnalysisServ;

/// <summary>Tests a seed against a definition with the full pass and collects where each condition was found.</summary>
public sealed class ConditionLocationsJobHandler(ISearchPlanCompiler planCompiler, ISeedMatcher seedMatcher) : IJobHandler<ConditionLocationsJob, ConditionLocations>
{
    public ConditionLocations Handle(ConditionLocationsJob job, IWorkerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        ArgumentNullException.ThrowIfNull(context);
        cancellationToken.ThrowIfCancellationRequested();

        var plan = planCompiler.Compile(job.Definition);
        var seedContext = new SearchSeedContext(context, job.Definition);
        seedContext.Open(job.Seed);
        var isMatch = seedMatcher.Evaluate(plan, seedContext, SearchPass.Full) == SearchOutcome.Passed;

        List<ConditionPlace> places = [];
        Collect(plan.Roots, places);
        return new ConditionLocations { Seed = job.Seed, IsMatch = isMatch, Places = [.. places.OrderBy(place => place.ConditionId)] };
    }

    private static void Collect(IReadOnlyList<PlanNode> nodes, List<ConditionPlace> places)
    {
        foreach (var node in nodes)
        {
            places.AddRange(node.Matches.Select(position => new ConditionPlace(node.Condition.Id, position)));
            Collect(node.Children, places);
        }
    }
}

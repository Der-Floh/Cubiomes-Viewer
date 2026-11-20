using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
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
        var seedContext = new SearchSeedContext(context, job.Definition, cancellationToken);
        seedContext.Open(job.Seed);
        var isMatch = seedMatcher.Evaluate(plan, seedContext, SearchPass.Full) == SearchOutcome.Passed;

        List<ConditionPlace> places = [];
        Collect(plan.Roots, job.Definition.Version, places);
        return new ConditionLocations { Seed = job.Seed, IsMatch = isMatch, Places = [.. places.OrderBy(place => place.ConditionId)] };
    }

    private static void Collect(IReadOnlyList<PlanNode> nodes, McVersion version, List<ConditionPlace> places)
    {
        foreach (var node in nodes)
        {
            var dimension = node.Evaluator.GetDimension(node.Condition, version);
            places.AddRange(node.Matches.Select(position => new ConditionPlace(node.Condition.Id, position, dimension)));
            Collect(node.Children, version, places);
        }
    }
}

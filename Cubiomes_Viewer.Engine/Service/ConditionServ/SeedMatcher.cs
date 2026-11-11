using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

public sealed class SeedMatcher : ISeedMatcher
{
    private static readonly BlockPos _origin = new(0, 0);

    public bool IsMatch(SearchPlan plan, SearchSeedContext context)
    {
        return Evaluate(plan, context, SearchPass.Quick) switch
        {
            SearchOutcome.Failed => false,
            SearchOutcome.Passed => true,
            _ => Evaluate(plan, context, SearchPass.Full) == SearchOutcome.Passed,
        };
    }

    public SearchOutcome Evaluate(SearchPlan plan, SearchSeedContext context, SearchPass pass)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(context);

        return EvaluateAll(plan.Roots, _origin, context, pass);
    }

    private static SearchOutcome EvaluateAll(IReadOnlyList<PlanNode> nodes, BlockPos reference, SearchSeedContext context, SearchPass pass)
    {
        var outcome = SearchOutcome.Passed;
        foreach (var node in nodes)
        {
            var result = EvaluateNode(node, reference, context, pass);
            if (result == SearchOutcome.Failed)
                return SearchOutcome.Failed;
            if (result == SearchOutcome.Undecided)
                outcome = SearchOutcome.Undecided;
        }

        return outcome;
    }

    private static SearchOutcome EvaluateAny(IReadOnlyList<PlanNode> nodes, BlockPos reference, SearchSeedContext context, SearchPass pass)
    {
        var outcome = SearchOutcome.Failed;
        foreach (var node in nodes)
        {
            var result = EvaluateNode(node, reference, context, pass);
            if (result == SearchOutcome.Passed)
                return SearchOutcome.Passed;
            if (result == SearchOutcome.Undecided)
                outcome = SearchOutcome.Undecided;
        }

        return outcome;
    }

    private static SearchOutcome EvaluateNode(PlanNode node, BlockPos reference, SearchSeedContext context, SearchPass pass)
    {
        node.Matches.Clear();
        var status = node.Evaluator.Evaluate(node.Condition, reference, context, pass, node.Matches);
        if (status == MatchStatus.Failed)
            return SearchOutcome.Failed;
        if (node.Children.Count == 0)
            return status == MatchStatus.Matched ? SearchOutcome.Passed : SearchOutcome.Undecided;

        return status switch
        {
            MatchStatus.Matched => EvaluateAtMatches(node, reference, context, pass),
            MatchStatus.Candidates => EvaluateAtCandidates(node, context, pass),
            _ => SearchOutcome.Undecided,
        };
    }

    private static SearchOutcome EvaluateAtMatches(PlanNode node, BlockPos reference, SearchSeedContext context, SearchPass pass)
    {
        return node.Condition.Anchor switch
        {
            MatchAnchor.Nearest => EvaluateChildren(node, GetNearest(node.Matches, reference), context, pass),
            MatchAnchor.Center => EvaluateChildren(node, GetCenter(node.Matches), context, pass),
            _ => EvaluateAtAnyPlace(node, context, pass),
        };
    }

    /// <summary>
    /// The matches are among the candidates, so the node fails when the conditions relative to it fail at every
    /// candidate; nothing else is certain. The middle of the matches is not even among the candidates.
    /// </summary>
    private static SearchOutcome EvaluateAtCandidates(PlanNode node, SearchSeedContext context, SearchPass pass)
    {
        if (node.Condition.Anchor == MatchAnchor.Center)
            return SearchOutcome.Undecided;

        return EvaluateAtAnyPlace(node, context, pass) == SearchOutcome.Failed ? SearchOutcome.Failed : SearchOutcome.Undecided;
    }

    private static SearchOutcome EvaluateAtAnyPlace(PlanNode node, SearchSeedContext context, SearchPass pass)
    {
        var outcome = SearchOutcome.Failed;
        foreach (var place in node.Matches)
        {
            var result = EvaluateChildren(node, place, context, pass);
            if (result == SearchOutcome.Passed)
                return SearchOutcome.Passed;
            if (result == SearchOutcome.Undecided)
                outcome = SearchOutcome.Undecided;
        }

        return outcome;
    }

    private static SearchOutcome EvaluateChildren(PlanNode node, BlockPos place, SearchSeedContext context, SearchPass pass)
        => node.AcceptsAnyChild ? EvaluateAny(node.Children, place, context, pass) : EvaluateAll(node.Children, place, context, pass);

    private static BlockPos GetNearest(List<BlockPos> matches, BlockPos reference)
    {
        var nearest = matches[0];
        foreach (var match in matches)
        {
            if (GetSquaredDistance(match, reference) < GetSquaredDistance(nearest, reference))
                nearest = match;
        }

        return nearest;
    }

    private static BlockPos GetCenter(List<BlockPos> matches)
    {
        long sumX = 0, sumZ = 0;
        foreach (var match in matches)
        {
            sumX += match.X;
            sumZ += match.Z;
        }

        return new BlockPos((int)(sumX / matches.Count), (int)(sumZ / matches.Count));
    }

    private static long GetSquaredDistance(BlockPos position, BlockPos other)
    {
        long x = (long)position.X - other.X;
        long z = (long)position.Z - other.Z;
        return x * x + z * z;
    }
}

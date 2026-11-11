using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>The base of the evaluators: it hands each one its own kind of condition and holds what they all decide alike.</summary>
public abstract class ConditionEvaluator<TCondition> : IConditionEvaluator where TCondition : SearchCondition
{
    public abstract ConditionDescriptor Descriptor { get; }

    public IEnumerable<string> Validate(SearchCondition condition, SearchDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        return Validate(Cast(condition), definition);
    }

    public MatchStatus Evaluate(SearchCondition condition, BlockPos reference, SearchSeedContext context, SearchPass pass, List<BlockPos> matches)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(matches);
        return Evaluate(Cast(condition), reference, context, pass, matches);
    }

    protected abstract IEnumerable<string> Validate(TCondition condition, SearchDefinition definition);

    protected abstract MatchStatus Evaluate(TCondition condition, BlockPos reference, SearchSeedContext context, SearchPass pass, List<BlockPos> matches);

    /// <summary>
    /// Decides a condition whose matches are all known: enough of them, or none of them for an exclusion.
    /// An exclusion that holds is found at its reference point.
    /// </summary>
    protected static MatchStatus Decide(SearchCondition condition, BlockPos reference, List<BlockPos> matches)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(matches);

        if (!condition.IsExclusion)
            return matches.Count >= condition.Count ? MatchStatus.Matched : MatchStatus.Failed;
        if (matches.Count > 0)
            return MatchStatus.Failed;

        matches.Add(reference);
        return MatchStatus.Matched;
    }

    /// <summary>
    /// Decides as much as places that may or may not be matches allow: too few of them fail the condition, and an
    /// exclusion already holds when there is none.
    /// </summary>
    protected static MatchStatus DecideCandidates(SearchCondition condition, BlockPos reference, List<BlockPos> candidates)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(candidates);

        if (!condition.IsExclusion)
            return candidates.Count >= condition.Count ? MatchStatus.Candidates : MatchStatus.Failed;

        var holds = candidates.Count == 0;
        candidates.Clear();
        candidates.Add(reference);
        return holds ? MatchStatus.Matched : MatchStatus.Candidates;
    }

    /// <summary>The middle of the rectangle a condition looks in.</summary>
    protected static BlockPos GetAreaCenter(SearchArea area, BlockPos reference)
        => new(reference.X + (area.MinX + area.MaxX) / 2, reference.Z + (area.MinZ + area.MaxZ) / 2);

    private static TCondition Cast(SearchCondition condition)
        => condition as TCondition ?? throw new ArgumentException($"Expected a {typeof(TCondition).Name} but got {condition?.GetType().Name ?? "nothing"}.", nameof(condition));
}

using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>
/// One kind of condition: how it is described, checked for mistakes and tested against a seed.
/// Evaluators are shared between workers and keep no state of their own.
/// </summary>
public interface IConditionEvaluator
{
    ConditionDescriptor Descriptor { get; }

    /// <summary>Lists what keeps a condition of this kind from being searched in the version of the definition.</summary>
    IEnumerable<string> Validate(SearchCondition condition, SearchDefinition definition);

    /// <summary>
    /// Looks for the matches of a condition around a reference point in the seed of the context, using only what the
    /// pass may use, and adds the places it found to <paramref name="matches"/>. The last pass always decides.
    /// </summary>
    MatchStatus Evaluate(SearchCondition condition, BlockPos reference, SearchSeedContext context, SearchPass pass, List<BlockPos> matches);
}

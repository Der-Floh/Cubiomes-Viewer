using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

public interface ISeedMatcher
{
    /// <summary>
    /// Whether the seed the context is open at meets every condition of the plan. The quick pass rejects or accepts
    /// what it can; only the seeds it leaves undecided get the full pass.
    /// </summary>
    bool IsMatch(SearchPlan plan, SearchSeedContext context);

    /// <summary>What one pass alone concludes about the seed the context is open at. The full pass always decides.</summary>
    SearchOutcome Evaluate(SearchPlan plan, SearchSeedContext context, SearchPass pass);
}

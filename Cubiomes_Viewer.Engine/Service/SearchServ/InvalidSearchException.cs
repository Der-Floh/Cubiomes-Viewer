using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Service.SearchServ;

/// <summary>A search was asked for that cannot run as it is set up.</summary>
public sealed class InvalidSearchException : Exception
{
    public IReadOnlyList<ConditionProblem> Problems { get; }

    public InvalidSearchException(IReadOnlyList<ConditionProblem> problems)
        : base(CreateMessage(problems))
    {
        Problems = problems;
    }

    private static string CreateMessage(IReadOnlyList<ConditionProblem> problems)
    {
        ArgumentNullException.ThrowIfNull(problems);
        return problems.Count == 0 ? "The search cannot run as it is set up." : string.Join(" ", problems);
    }
}

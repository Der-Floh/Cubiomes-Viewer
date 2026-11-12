using Cubiomes_Viewer.Cli.Data;

namespace Cubiomes_Viewer.Cli.Service.CommandServ;

public interface ISearchCommand
{
    /// <summary>Runs or continues the search of a session file until it finishes or <paramref name="cancellationToken"/> stops it.</summary>
    /// <returns>The exit code of the process.</returns>
    Task<int> RunAsync(SearchRequest request, CancellationToken cancellationToken = default);
}

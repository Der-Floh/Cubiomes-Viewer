namespace Cubiomes_Viewer.Cli.Service.CommandServ;

public interface ICliApplication
{
    /// <summary>Does what the command line asks for.</summary>
    /// <returns>The exit code of the process.</returns>
    Task<int> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default);
}

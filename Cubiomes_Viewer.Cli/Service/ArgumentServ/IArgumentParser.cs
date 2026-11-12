using Cubiomes_Viewer.Cli.Data;

namespace Cubiomes_Viewer.Cli.Service.ArgumentServ;

public interface IArgumentParser
{
    /// <summary>Reads what the command line asks for. A request for help wins over everything else on the line.</summary>
    /// <exception cref="InvalidArgumentsException">The command line asks for something the CLI does not offer, or leaves out what it needs.</exception>
    CliRequest Parse(IReadOnlyList<string> arguments);
}

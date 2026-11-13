using System.CommandLine;

namespace Cubiomes_Viewer.Cli.Service.CommandLineServ;

public interface ICliCommandLine
{
    /// <summary>
    /// Reads the command line. The result lists what is wrong with it, and invoking it runs what it asks for: a search,
    /// the self-test, the help or the version.
    /// </summary>
    ParseResult Parse(IReadOnlyList<string> arguments);
}

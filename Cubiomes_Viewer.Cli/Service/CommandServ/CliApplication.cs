using System.CommandLine;
using System.CommandLine.Parsing;

using Cubiomes_Viewer.Cli.Data;
using Cubiomes_Viewer.Cli.Service.CommandLineServ;
using Cubiomes_Viewer.Cli.Service.ConsoleServ;

namespace Cubiomes_Viewer.Cli.Service.CommandServ;

/// <summary>Reads the command line, runs what it asks for and reports invalid arguments and failures with files as messages.</summary>
public sealed class CliApplication : ICliApplication
{
    private const string HelpHint = $"Run with {CliOptionNames.Help} to see the options.";

    private readonly ICliCommandLine _commandLine;
    private readonly ICliConsole _console;

    /// <summary>The process stops searches itself, on its own signal handlers, and exceptions reach this class.</summary>
    private readonly InvocationConfiguration _invocation;

    public CliApplication(ICliCommandLine commandLine, ICliConsole console)
    {
        _commandLine = commandLine;
        _console = console;
        _invocation = new InvocationConfiguration { Output = console.Output, EnableDefaultExceptionHandler = false, ProcessTerminationTimeout = null };
    }

    public async Task<int> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);

        var parseResult = _commandLine.Parse(arguments);
        if (parseResult.Errors.Count > 0)
            return ReportInvalidArguments(parseResult.Errors);

        try
        {
            return await parseResult.InvokeAsync(_invocation, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _console.WriteMessage(ex.Message);
            return ExitCodes.Failure;
        }
    }

    private int ReportInvalidArguments(IReadOnlyList<ParseError> errors)
    {
        foreach (var error in errors)
            _console.WriteMessage(error.Message);
        _console.WriteMessage(HelpHint);
        return ExitCodes.InvalidArguments;
    }
}

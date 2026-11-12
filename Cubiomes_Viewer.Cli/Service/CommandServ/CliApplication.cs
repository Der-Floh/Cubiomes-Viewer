using System.Diagnostics.CodeAnalysis;
using System.Reflection;

using Cubiomes_Viewer.Cli.Data;
using Cubiomes_Viewer.Cli.Service.ArgumentServ;
using Cubiomes_Viewer.Cli.Service.ConsoleServ;

namespace Cubiomes_Viewer.Cli.Service.CommandServ;

/// <summary>Reads the command line, runs the command it asks for and reports failures with files as messages.</summary>
public sealed class CliApplication : ICliApplication
{
    private static readonly Assembly _assembly = typeof(CliApplication).Assembly;
    private static readonly string _name = _assembly.GetName().Name ?? nameof(CliApplication);
    private static readonly string _version = _assembly.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion ?? "unknown";

    private readonly IArgumentParser _parser;
    private readonly ISearchCommand _search;
    private readonly ISelfTestCommand _selfTest;
    private readonly ICliConsole _console;

    public CliApplication(IArgumentParser parser, ISearchCommand search, ISelfTestCommand selfTest, ICliConsole console)
    {
        _parser = parser;
        _search = search;
        _selfTest = selfTest;
        _console = console;
    }

    public async Task<int> RunAsync(IReadOnlyList<string> arguments, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        if (!TryParse(arguments, out var request))
            return ExitCodes.InvalidArguments;

        try
        {
            return await RunAsync(request, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _console.WriteMessage(ex.Message);
            return ExitCodes.Failure;
        }
    }

    private bool TryParse(IReadOnlyList<string> arguments, [NotNullWhen(true)] out CliRequest? request)
    {
        try
        {
            request = _parser.Parse(arguments);
            return true;
        }
        catch (InvalidArgumentsException ex)
        {
            _console.WriteMessage($"{ex.Message} Run with {CliOptionNames.Help} to see the options.");
            request = null;
            return false;
        }
    }

    private async Task<int> RunAsync(CliRequest request, CancellationToken cancellationToken) => request switch
    {
        HelpRequest => Show(CreateHelp()),
        VersionRequest => Show($"{_name} {_version}"),
        SelfTestRequest => _selfTest.Run(),
        SearchRequest search => await _search.RunAsync(search, cancellationToken),
        _ => throw new NotSupportedException($"Unsupported request: {request}"),
    };

    private int Show(string text)
    {
        _console.Output.WriteLine(text);
        return ExitCodes.Success;
    }

    private static string CreateHelp() => $"""
        Cubiomes Viewer CLI {_version}
        Runs a search saved with "Save to file" in the Search panel of Cubiomes Viewer,
        without the app.

        Usage:
          {_name} {CliOptionNames.Session} <file> [{CliOptionNames.Out} <file>] [{CliOptionNames.Reset}]
          {_name} {CliOptionNames.SelfTest}

        Options:
          {CliOptionNames.Session} <file>  The search session to run. Its progress and the seeds found
                            are saved back into it, so running it again continues the
                            search. A search that finished only lists its seeds.
          {CliOptionNames.Out} <file>      Writes the seeds found to the file, one per line, instead of
                            to the standard output.
          {CliOptionNames.Reset}           Starts the search over instead of continuing it.
          {CliOptionNames.SelfTest}        Checks that the generator on this machine produces the
                            reference results.
          {CliOptionNames.Version}         Shows the version.
          {CliOptionNames.ShortHelp}, {CliOptionNames.Help}        Shows this help.

        Ctrl+C stops the search and saves where it stopped; a second Ctrl+C ends at once.
        Exit codes: 0 finished, 1 failed, 2 invalid arguments, 130 stopped before finishing.
        """;
}

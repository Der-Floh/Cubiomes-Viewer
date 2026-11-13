using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;

using Cubiomes_Viewer.Cli.Data;
using Cubiomes_Viewer.Cli.Service.CommandServ;

namespace Cubiomes_Viewer.Cli.Service.CommandLineServ;

/// <summary>
/// The options of the CLI and the checks of how they combine. <c>--session</c> runs a search; <c>--selftest</c>, like
/// <c>--help</c> and <c>--version</c>, does something else and stands alone.
/// </summary>
public sealed class CliCommandLine : ICliCommandLine
{
    private const string FileHelpName = "file";

    private const string Description = """
        Runs a search saved with "Save to file" in the Search panel of Cubiomes Viewer, without the app.
        Ctrl+C stops the search and saves where it stopped; a second Ctrl+C ends at once.
        Exit codes: 0 finished, 1 failed, 2 invalid arguments, 130 stopped before finishing.
        """;

    /// <summary>Paths that differ only in case name the same file where the file system ignores case.</summary>
    private static readonly StringComparison _pathComparison = OperatingSystem.IsWindows() || OperatingSystem.IsMacOS() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

    private readonly Option<string> _session = new(CliOptionNames.Session)
    {
        Description = "The search session to run. Its progress and the seeds found are saved back into it, so running it again continues the search. A search that finished only lists its seeds.",
        HelpName = FileHelpName,
        Required = true,
    };

    private readonly Option<string> _out = new(CliOptionNames.Out)
    {
        Description = "Writes the seeds found to the file, one per line, instead of to the standard output.",
        HelpName = FileHelpName,
    };

    private readonly Option<bool> _reset = new(CliOptionNames.Reset) { Description = "Starts the search over instead of continuing it." };
    private readonly Option<bool> _selfTest = new(CliOptionNames.SelfTest) { Description = "Checks that the generator on this machine produces the reference results." };
    private readonly RootCommand _root = new(Description);
    private readonly ISearchCommand _search;

    public CliCommandLine(ISearchCommand search, ISelfTestCommand selfTest)
    {
        _search = search;

        _session.Validators.Add(RequireFile);
        _out.Validators.Add(RequireFile);
        _out.Validators.Add(RefuseTheSessionFile);
        _selfTest.Validators.Add(RequireAlone);
        _selfTest.Action = new SelfTestAction(selfTest);

        _root.Options.Add(_session);
        _root.Options.Add(_out);
        _root.Options.Add(_reset);
        _root.Options.Add(_selfTest);
        _root.SetAction(SearchAsync);
    }

    public ParseResult Parse(IReadOnlyList<string> arguments) => _root.Parse(arguments);

    private Task<int> SearchAsync(ParseResult result, CancellationToken cancellationToken)
        => _search.RunAsync(new SearchRequest { SessionPath = result.GetRequiredValue(_session), OutputPath = result.GetValue(_out), Reset = result.GetValue(_reset) }, cancellationToken);

    private static void RequireFile(OptionResult result)
    {
        if (result.Tokens is [{ Value: var path }] && string.IsNullOrWhiteSpace(path))
            result.AddError($"{result.Option.Name} needs a file.");
    }

    private void RefuseTheSessionFile(OptionResult result)
    {
        if (result.GetResult(_session) is { Tokens: [{ Value: var sessionPath }] } && result.Tokens is [{ Value: var outputPath }] && IsSameFile(sessionPath, outputPath))
            result.AddError("The seeds cannot go to the session file itself.");
    }

    private static void RequireAlone(OptionResult result)
    {
        if (!result.Implicit && result.Parent is CommandResult command && command.Children.Any(child => child != result && child is OptionResult { Implicit: false }))
            result.AddError($"{result.Option.Name} cannot be combined with other options.");
    }

    private static bool IsSameFile(string first, string second)
        => !string.IsNullOrWhiteSpace(first) && !string.IsNullOrWhiteSpace(second) && string.Equals(Path.GetFullPath(first), Path.GetFullPath(second), _pathComparison);

    /// <summary>
    /// Runs the self-test instead of a search. Like the actions of <c>--help</c> and <c>--version</c>, it clears the
    /// errors that do not concern it, such as the missing <c>--session</c>.
    /// </summary>
    private sealed class SelfTestAction(ISelfTestCommand selfTest) : SynchronousCommandLineAction
    {
        public override bool ClearsParseErrors => true;

        public override int Invoke(ParseResult parseResult) => selfTest.Run();
    }
}

using System.CommandLine;
using System.Globalization;

using Cubiomes_Viewer.Cli.Data;
using Cubiomes_Viewer.Cli.Service.CommandLineServ;
using Cubiomes_Viewer.Cli.Service.CommandServ;

namespace Cubiomes_Viewer.Cli.Tests.Service.CommandLineServ;

public sealed class CliCommandLineTests
{
    private readonly RecordingSearchCommand _search = new();
    private readonly CountingSelfTestCommand _selfTest = new();
    private readonly CliCommandLine _commandLine;

    public CliCommandLineTests() => _commandLine = new CliCommandLine(_search, _selfTest);

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public static TheoryData<string[], string> InvalidCommandLines() => new()
    {
        { [], "Option '--session' is required." },
        { ["--frobnicate"], "Unrecognized command or argument '--frobnicate'." },
        { ["search.json"], "Unrecognized command or argument 'search.json'." },
        { ["--session"], "Required argument missing for option: '--session'." },
        { ["--session="], "Required argument missing for option: '--session'." },
        { ["--session", " "], "--session needs a file." },
        { ["--session", "a.json", "--out", ""], "--out needs a file." },
        { ["--session", "a.json", "--session", "b.json"], "Option '--session' expects a single argument but 2 were provided." },
        { ["--session", "a.json", "--reset=yes"], "Unrecognized command or argument 'yes'." },
        { ["--selftest", "--session", "a.json"], "--selftest cannot be combined with other options." },
        { ["--reset", "--selftest"], "--selftest cannot be combined with other options." },
        { ["--version", "--selftest"], "--selftest cannot be combined with other options." },
        { ["--version", "--reset"], "--version option cannot be combined with other arguments." },
        { ["--session", "a.json", "--out", "a.json"], "The seeds cannot go to the session file itself." },
    };

    [Fact]
    public async Task SessionOnly_SearchesItIntoTheOutput()
    {
        await InvokeAsync(["--session", "search.json"]);

        Assert.Equal(new SearchRequest { SessionPath = "search.json" }, _search.Request);
    }

    [Fact]
    public async Task EveryOptionOfASearch_InAnyOrderAndEitherForm()
    {
        await InvokeAsync(["--reset", "--out=seeds.txt", "--session", "search.json"]);

        Assert.Equal(new SearchRequest { SessionPath = "search.json", OutputPath = "seeds.txt", Reset = true }, _search.Request);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    [InlineData("-?")]
    public async Task HelpAnywhere_ShowsTheHelpInsteadOfSearching(string help)
    {
        var output = new StringWriter();

        var exitCode = await InvokeAsync(["--session", "search.json", help, "--frobnicate"], output);

        Assert.Equal(ExitCodes.Success, exitCode);
        Assert.Contains(CliOptionNames.Session, output.ToString(), StringComparison.Ordinal);
        Assert.Null(_search.Request);
    }

    [Fact]
    public async Task SelfTest_RunsInsteadOfASearch()
    {
        await InvokeAsync([CliOptionNames.SelfTest]);

        Assert.Equal(1, _selfTest.Runs);
        Assert.Null(_search.Request);
    }

    [Theory]
    [MemberData(nameof(InvalidCommandLines))]
    public void InvalidCommandLine_SaysWhatIsWrong(string[] arguments, string message)
        => Assert.Contains(message, _commandLine.Parse(arguments).Errors.Select(error => error.Message));

    [Fact]
    public void SeedFileThatIsTheSessionByAnotherPath_IsRefused()
    {
        var sessionPath = Path.Combine(Path.GetTempPath(), "search.json");
        var outputPath = Path.Combine(Path.GetTempPath(), ".", "search.json");

        Assert.NotEmpty(_commandLine.Parse(["--session", sessionPath, "--out", outputPath]).Errors);
    }

    [Fact]
    public void Errors_OnAMachineInAnotherLanguage_AreInEnglish()
    {
        var language = CultureInfo.CurrentUICulture;
        CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("de-DE");
        try
        {
            var error = Assert.Single(_commandLine.Parse(["--session", "a.json", "--frobnicate"]).Errors);

            Assert.Equal("Unrecognized command or argument '--frobnicate'.", error.Message);
        }
        finally
        {
            CultureInfo.CurrentUICulture = language;
        }
    }

    private Task<int> InvokeAsync(string[] arguments, TextWriter? output = null)
    {
        var parseResult = _commandLine.Parse(arguments);
        Assert.Empty(parseResult.Errors);
        return parseResult.InvokeAsync(new InvocationConfiguration { Output = output ?? TextWriter.Null }, Token);
    }

    private sealed class RecordingSearchCommand : ISearchCommand
    {
        public SearchRequest? Request { get; private set; }

        public Task<int> RunAsync(SearchRequest request, CancellationToken cancellationToken = default)
        {
            Request = request;
            return Task.FromResult(ExitCodes.Success);
        }
    }

    private sealed class CountingSelfTestCommand : ISelfTestCommand
    {
        public int Runs { get; private set; }

        public int Run()
        {
            Runs++;
            return ExitCodes.Success;
        }
    }
}

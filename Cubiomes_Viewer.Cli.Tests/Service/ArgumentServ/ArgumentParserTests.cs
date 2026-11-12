using Cubiomes_Viewer.Cli.Data;
using Cubiomes_Viewer.Cli.Service.ArgumentServ;

namespace Cubiomes_Viewer.Cli.Tests.Service.ArgumentServ;

public sealed class ArgumentParserTests
{
    private readonly ArgumentParser _parser = new();

    public static TheoryData<string[], string> InvalidCommandLines() => new()
    {
        { [], "Name the search session to run with --session <file>." },
        { ["--frobnicate"], "Unknown option '--frobnicate'." },
        { ["search.json"], "Unexpected argument 'search.json'." },
        { ["--session"], "--session needs a file." },
        { ["--session", "--reset"], "--session needs a file." },
        { ["--session="], "--session needs a file." },
        { ["--session", "a.json", "--session", "b.json"], "--session is given more than once." },
        { ["--session", "a.json", "--reset=yes"], "--reset takes no value." },
        { ["--selftest", "--session", "a.json"], "--selftest cannot be combined with other options." },
        { ["--version", "--selftest"], "--version cannot be combined with other options." },
        { ["--session", "a.json", "--out", "a.json"], "The seeds cannot go to the session file itself." },
    };

    [Fact]
    public void Parse_SessionOnly_SearchesItIntoTheOutput()
    {
        var request = _parser.Parse(["--session", "search.json"]);

        Assert.Equal(new SearchRequest { SessionPath = "search.json" }, request);
    }

    [Fact]
    public void Parse_EveryOptionOfASearch_InAnyOrderAndEitherForm()
    {
        var request = _parser.Parse(["--reset", "--out=seeds.txt", "--session", "search.json"]);

        Assert.Equal(new SearchRequest { SessionPath = "search.json", OutputPath = "seeds.txt", Reset = true }, request);
    }

    [Theory]
    [InlineData("--help")]
    [InlineData("-h")]
    public void Parse_HelpAnywhere_AsksForHelp(string help)
        => Assert.IsType<HelpRequest>(_parser.Parse(["--session", "search.json", help, "--frobnicate"]));

    [Fact]
    public void Parse_SelfTest_AsksForTheSelfTest() => Assert.IsType<SelfTestRequest>(_parser.Parse(["--selftest"]));

    [Fact]
    public void Parse_Version_AsksForTheVersion() => Assert.IsType<VersionRequest>(_parser.Parse(["--version"]));

    [Theory]
    [MemberData(nameof(InvalidCommandLines))]
    public void Parse_InvalidCommandLine_SaysWhatIsWrong(string[] arguments, string message)
    {
        var exception = Assert.Throws<InvalidArgumentsException>(() => _parser.Parse(arguments));

        Assert.Equal(message, exception.Message);
    }

    [Fact]
    public void Parse_SeedFileThatIsTheSessionByAnotherPath_IsRefused()
    {
        var sessionPath = Path.Combine(Path.GetTempPath(), "search.json");
        var outputPath = Path.Combine(Path.GetTempPath(), ".", "search.json");

        Assert.Throws<InvalidArgumentsException>(() => _parser.Parse(["--session", sessionPath, "--out", outputPath]));
    }
}

using Cubiomes_Viewer.Cli.Data;

namespace Cubiomes_Viewer.Cli.Tests.Service.CommandServ;

public sealed class CliApplicationTests : IDisposable
{
    private readonly string _appData = Path.Combine(Path.GetTempPath(), "cubiomes-viewer-tests", Guid.NewGuid().ToString("N"));

    private static CancellationToken Token => TestContext.Current.CancellationToken;

    public void Dispose()
    {
        if (Directory.Exists(_appData))
            Directory.Delete(_appData, recursive: true);
    }

    [Fact]
    public async Task Help_ListsEveryOption()
    {
        string[] options = [CliOptionNames.Session, CliOptionNames.Out, CliOptionNames.Reset, CliOptionNames.SelfTest, CliOptionNames.Version, CliOptionNames.Help];

        var result = await CliRun.RunAsync(_appData, ["--help"], cancellationToken: Token);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.All(options, option => Assert.Contains(option, result.Output));
    }

    [Fact]
    public async Task Version_ShowsTheCommandAndItsVersion()
    {
        var result = await CliRun.RunAsync(_appData, ["--version"], cancellationToken: Token);

        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.StartsWith("cubiomes-viewer-cli 0.0.0", result.Output);
    }

    [Theory]
    [InlineData("")]
    [InlineData("--frobnicate")]
    [InlineData("--session")]
    [InlineData("search.json")]
    [InlineData("--version --reset")]
    public async Task InvalidCommandLine_FailsWithAPointerToTheHelp(string commandLine)
    {
        var result = await CliRun.RunAsync(_appData, commandLine.Split(' ', StringSplitOptions.RemoveEmptyEntries), cancellationToken: Token);

        Assert.Equal(ExitCodes.InvalidArguments, result.ExitCode);
        Assert.EndsWith($"Run with {CliOptionNames.Help} to see the options.", result.Messages.TrimEnd());
        Assert.Empty(result.Output);
    }

    [Fact]
    public async Task SelfTest_OnThisMachine_PassesEveryCheck()
    {
        var result = await CliRun.RunAsync(_appData, ["--selftest"], cancellationToken: Token);

        var lines = result.Output.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        Assert.Equal(ExitCodes.Success, result.ExitCode);
        Assert.True(lines.Length > 1);
        Assert.All(lines[..^1], line => Assert.StartsWith("PASS ", line));
        Assert.Matches(@"^(\d+)/\1 checks passed in \d+ ms$", lines[^1]);
    }
}

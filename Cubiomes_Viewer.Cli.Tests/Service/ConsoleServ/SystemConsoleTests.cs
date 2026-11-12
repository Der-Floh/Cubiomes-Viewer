using Cubiomes_Viewer.Cli.Service.ConsoleServ;

namespace Cubiomes_Viewer.Cli.Tests.Service.ConsoleServ;

public sealed class SystemConsoleTests
{
    private readonly StringWriter _terminal = new() { NewLine = "\n" };

    [Fact]
    public void WriteStatus_InATerminal_ReplacesTheStatusBeforeInItsLine()
    {
        var console = CreateTerminal(width: 80);

        console.WriteStatus("12 seeds found");
        console.WriteStatus("13 found");

        Assert.Equal("\r12 seeds found\r13 found      ", _terminal.ToString());
    }

    [Fact]
    public void WriteMessage_WhileAStatusIsShown_ClearsItsLineFirst()
    {
        var console = CreateTerminal(width: 80);

        console.WriteStatus("status");
        console.WriteMessage("message");
        console.WriteStatus("next");

        Assert.Equal("\rstatus\r      \rmessage\n\rnext", _terminal.ToString());
    }

    [Fact]
    public void ClearStatus_RemovesTheStatusOnlyOnce()
    {
        var console = CreateTerminal(width: 80);

        console.ClearStatus();
        console.WriteStatus("status");
        console.ClearStatus();
        console.ClearStatus();

        Assert.Equal("\rstatus\r      \r", _terminal.ToString());
    }

    [Fact]
    public void WriteStatus_LongerThanTheTerminalIsWide_IsCutToFitIntoOneLine()
    {
        var console = CreateTerminal(width: 8);

        console.WriteStatus("0123456789");

        Assert.Equal("\r0123456", _terminal.ToString());
    }

    [Fact]
    public void Output_InTheTerminalOfTheStatus_ClearsTheStatusFirst()
    {
        var console = new SystemConsole(_terminal, _terminal, canReplaceStatus: true, isOutputInTerminal: true, () => 80);

        console.WriteStatus("status");
        console.Output.WriteLine("676");
        console.Output.WriteLine("694");
        console.WriteStatus("next");

        Assert.Equal("\rstatus\r      \r676\n694\n\rnext", _terminal.ToString());
    }

    [Fact]
    public void Output_RedirectedElsewhere_LeavesTheStatusAlone()
    {
        var output = new StringWriter();
        var console = new SystemConsole(output, _terminal, canReplaceStatus: true, isOutputInTerminal: false, () => 80);

        console.WriteStatus("status");
        console.Output.WriteLine("676");

        Assert.Equal("\rstatus", _terminal.ToString());
        Assert.Same(output, console.Output);
    }

    [Fact]
    public void WriteStatus_ErrorStreamRedirected_AddsALineForEachStatus()
    {
        var console = new SystemConsole(TextWriter.Null, _terminal, canReplaceStatus: false, isOutputInTerminal: true, () => 80);

        console.WriteStatus("first");
        console.WriteStatus("second");
        console.ClearStatus();

        Assert.Equal("first\nsecond\n", _terminal.ToString());
    }

    private SystemConsole CreateTerminal(int width) => new(TextWriter.Null, _terminal, canReplaceStatus: true, isOutputInTerminal: false, () => width);
}

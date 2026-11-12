using Cubiomes_Viewer.Cli.Data;
using Cubiomes_Viewer.Cli.Service.ConsoleServ;
using Cubiomes_Viewer.Engine.Service.SelfTestServ;

namespace Cubiomes_Viewer.Cli.Service.CommandServ;

public sealed class SelfTestCommand : ISelfTestCommand
{
    private readonly ISelfTestService _selfTest;
    private readonly ICliConsole _console;

    public SelfTestCommand(ISelfTestService selfTest, ICliConsole console)
    {
        _selfTest = selfTest;
        _console = console;
    }

    public int Run()
    {
        var report = _selfTest.Run();
        foreach (var check in report.Checks)
            _console.Output.WriteLine(check.ToString());
        _console.Output.WriteLine(report.ToString());
        return report.Passed ? ExitCodes.Success : ExitCodes.Failure;
    }
}

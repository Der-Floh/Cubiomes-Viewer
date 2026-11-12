namespace Cubiomes_Viewer.Cli.Service.CommandServ;

public interface ISelfTestCommand
{
    /// <summary>Checks the generator against the reference results and lists every check.</summary>
    /// <returns>The exit code of the process.</returns>
    int Run();
}

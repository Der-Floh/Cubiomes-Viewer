using Cubiomes_Viewer.Cli.Extensions;
using Cubiomes_Viewer.Cli.Service.CommandServ;
using Cubiomes_Viewer.Cli.Service.ConsoleServ;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Cli.Tests;

/// <summary>Runs the CLI as its process does, with app data in a folder of the test's and the streams it writes kept.</summary>
internal static class CliRun
{
    public static async Task<CliResult> RunAsync(string appData, IReadOnlyList<string> arguments, StringWriter? output = null, CancellationToken cancellationToken = default)
    {
        output ??= new StringWriter();
        var messages = new StringWriter();
        var console = new SystemConsole(output, messages, canReplaceStatus: false, isOutputInTerminal: false, getWidth: () => 0);
        await using var services = new ServiceCollection()
            .AddCubiomesEngine()
            .AddCubiomesCli(console)
            .AddSingleton<IAppDataStore>(new FileSystemAppDataStore(appData))
            .BuildServiceProvider();

        var exitCode = await services.GetRequiredService<ICliApplication>().RunAsync(arguments, cancellationToken);
        return new CliResult(exitCode, output.ToString(), messages.ToString());
    }
}

/// <param name="ExitCode">What the process would return.</param>
/// <param name="Output">What the CLI wrote to the standard output.</param>
/// <param name="Messages">What the CLI wrote to the error stream.</param>
internal sealed record CliResult(int ExitCode, string Output, string Messages)
{
    public IReadOnlyList<long> Seeds => FixtureSessions.ParseSeeds(Output);
}

/// <summary>Stops the CLI as soon as it writes its first seed, as Ctrl+C would.</summary>
internal sealed class StoppingWriter(CancellationTokenSource stopping) : StringWriter
{
    public override void WriteLine(string? value)
    {
        base.WriteLine(value);
        stopping.Cancel();
    }
}

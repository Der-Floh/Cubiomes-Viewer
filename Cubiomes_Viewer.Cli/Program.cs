using System.Runtime.InteropServices;

using Cubiomes_Viewer.Cli.Extensions;
using Cubiomes_Viewer.Cli.Service.CommandServ;
using Cubiomes_Viewer.Cli.Service.ConsoleServ;
using Cubiomes_Viewer.Engine.Extensions;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Cli;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        using var stopping = new CancellationTokenSource();
        using var interrupt = PosixSignalRegistration.Create(PosixSignal.SIGINT, context => Stop(context, stopping));
        using var termination = PosixSignalRegistration.Create(PosixSignal.SIGTERM, context => Stop(context, stopping));

        await using var services = new ServiceCollection()
            .AddCubiomesEngine()
            .AddCubiomesCli(SystemConsole.Create())
            .BuildServiceProvider();
        return await services.GetRequiredService<ICliApplication>().RunAsync(args, stopping.Token);
    }

    /// <summary>The first signal stops the search, which then saves where it stopped; a second one ends the process at once.</summary>
    private static void Stop(PosixSignalContext context, CancellationTokenSource stopping)
    {
        if (stopping.IsCancellationRequested)
            return;

        context.Cancel = true;
        stopping.Cancel();
    }
}

using System.Runtime.InteropServices.JavaScript;

using Cubiomes_Viewer.Browser.Interop;

namespace Cubiomes_Viewer.Browser.Service;

/// <summary>
/// Boots engine workers and reports their start-up and generation times to the browser console.
/// Enabled by opening the app with <c>?workertest</c> in the URL.
/// </summary>
internal static class WorkerDiagnostics
{
    public const string UrlSwitch = "workertest";

    private const int WorkerCount = 2;
    private const int SampleSeed = 262;
    private const int TileSize = 128;
    private static readonly int[] _tileScales = [4, 16, 64];

    public static async Task RunAsync()
    {
        await JSHost.ImportAsync(WorkerInterop.ModuleName, WorkerInterop.ModuleUrl);

        for (var workerId = 0; workerId < WorkerCount; workerId++)
        {
            var bootMilliseconds = await WorkerInterop.CreateWorkerAsync(workerId, WorkerInterop.WorkerScriptUrl);
            Console.WriteLine($"[workertest] worker {workerId} booted in {bootMilliseconds:F0} ms");
            Console.WriteLine($"[workertest] worker {workerId} self-test: {await WorkerInterop.RunSelfTestAsync(workerId)}");

            foreach (var scale in _tileScales)
                Console.WriteLine($"[workertest] worker {workerId} tile 1:{scale}: {await WorkerInterop.MeasureBiomeTileAsync(workerId, SampleSeed, scale, TileSize)}");
        }

        for (var workerId = 0; workerId < WorkerCount; workerId++)
            WorkerInterop.Terminate(workerId);

        Console.WriteLine("[workertest] done");
    }
}

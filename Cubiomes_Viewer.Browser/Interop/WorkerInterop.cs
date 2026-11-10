using System.Runtime.InteropServices.JavaScript;

namespace Cubiomes_Viewer.Browser.Interop;

internal static partial class WorkerInterop
{
    public const string ModuleName = "cv-workers";
    public const string ModuleUrl = "../cv-workers.js";
    public const string WorkerScriptUrl = "worker/worker.js";

    [JSImport("createWorker", ModuleName)]
    internal static partial Task<double> CreateWorkerAsync(int workerId, string scriptUrl);

    [JSImport("runSelfTest", ModuleName)]
    internal static partial Task<string> RunSelfTestAsync(int workerId);

    [JSImport("measureBiomeTile", ModuleName)]
    internal static partial Task<string> MeasureBiomeTileAsync(int workerId, int seed, int scale, int size);

    [JSImport("terminate", ModuleName)]
    internal static partial void Terminate(int workerId);
}

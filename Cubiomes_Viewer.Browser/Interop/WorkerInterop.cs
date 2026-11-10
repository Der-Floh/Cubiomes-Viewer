using System.Runtime.InteropServices.JavaScript;

namespace Cubiomes_Viewer.Browser.Interop;

internal static partial class WorkerInterop
{
    public const string ModuleName = "cv-workers";
    public const string ModuleUrl = "../cv-workers.js";
    public const string WorkerScriptUrl = "worker/worker.js";
    public const string ResponseProperty = "response";

    /// <summary>Starts a worker and returns its start-up time in milliseconds.</summary>
    [JSImport("createWorker", ModuleName)]
    internal static partial Task<double> CreateWorkerAsync(int workerId, string scriptUrl);

    /// <summary>Runs one serialized job; the worker's reply holds the response bytes in <see cref="ResponseProperty"/>.</summary>
    [JSImport("execute", ModuleName)]
    internal static partial Task<JSObject> ExecuteAsync(int workerId, byte[] request);

    [JSImport("terminate", ModuleName)]
    internal static partial void Terminate(int workerId);
}

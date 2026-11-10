using System.Runtime.InteropServices.JavaScript;

using Cubiomes_Viewer.Browser.Interop;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Browser.Service;

internal sealed class WebWorkerFactory : IComputeWorkerFactory
{
    private const int MaxWorkers = 6;
    private const string NavigatorProperty = "navigator";
    private const string HardwareConcurrencyProperty = "hardwareConcurrency";
    private const string NumberType = "number";

    /// <summary>One logical processor stays free for the page itself; every worker hosts a whole .NET runtime, hence the cap.</summary>
    public int WorkerCount { get; } = Math.Clamp(GetHardwareConcurrency() - 1, 1, MaxWorkers);

    private readonly ILogger<WebWorkerFactory> _logger;
    private Task<JSObject>? _moduleImport;
    private int _nextWorkerId;

    public WebWorkerFactory(ILogger<WebWorkerFactory> logger)
    {
        _logger = logger;
    }

    public async Task<IComputeWorker> StartWorkerAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var workerId = _nextWorkerId++;
        try
        {
            await (_moduleImport ??= JSHost.ImportAsync(WorkerInterop.ModuleName, WorkerInterop.ModuleUrl));
            var bootMilliseconds = await WorkerInterop.CreateWorkerAsync(workerId, WorkerInterop.WorkerScriptUrl);
            _logger.LogDebug("Web Worker {WorkerId} started in {BootMilliseconds:F0} ms", workerId, bootMilliseconds);
            return new WebWorker(workerId);
        }
        catch (JSException ex)
        {
            throw new ComputeWorkerException($"Web Worker {workerId} could not be started: {ex.Message}", ex);
        }
    }

    private static int GetHardwareConcurrency()
    {
        using var navigator = JSHost.GlobalThis.GetPropertyAsJSObject(NavigatorProperty);
        return navigator?.GetTypeOfProperty(HardwareConcurrencyProperty) == NumberType
            ? navigator.GetPropertyAsInt32(HardwareConcurrencyProperty)
            : 1;
    }
}

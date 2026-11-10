using System.Runtime.InteropServices.JavaScript;

using Cubiomes_Viewer.Browser.Interop;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

namespace Cubiomes_Viewer.Browser.Service;

/// <summary>A Web Worker that hosts its own .NET runtime with the engine.</summary>
internal sealed class WebWorker : IComputeWorker
{
    private readonly int _workerId;

    public WebWorker(int workerId)
    {
        _workerId = workerId;
    }

    public async Task<byte[]> ExecuteAsync(byte[] request)
    {
        try
        {
            using var reply = await WorkerInterop.ExecuteAsync(_workerId, request);
            return reply.GetPropertyAsByteArray(WorkerInterop.ResponseProperty)
                ?? throw new ComputeWorkerException($"Web Worker {_workerId} replied without a response.");
        }
        catch (JSException ex)
        {
            throw new ComputeWorkerException($"Web Worker {_workerId} failed: {ex.Message}", ex);
        }
    }

    public void Dispose() => WorkerInterop.Terminate(_workerId);
}

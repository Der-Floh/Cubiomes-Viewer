namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

/// <summary>
/// Executes serialized jobs inside this runtime, yielding before each one so the caller's thread stays responsive.
/// Stands in when no worker in another runtime is available.
/// </summary>
internal sealed class LocalComputeWorker : IComputeWorker
{
    private readonly ComputeWorkerHost _host;

    public LocalComputeWorker(ComputeWorkerHost host)
    {
        _host = host;
    }

    public async Task<byte[]> ExecuteAsync(byte[] request)
    {
        await Task.Yield();
        return _host.Execute(request);
    }

    public void Dispose() => _host.Dispose();
}

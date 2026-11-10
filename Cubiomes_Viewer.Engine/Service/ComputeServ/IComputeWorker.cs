namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

/// <summary>
/// A worker in another runtime, such as a Web Worker, that executes one serialized job at a time
/// with a <see cref="ComputeWorkerHost"/> on its side. Disposing stops the worker and may be repeated.
/// </summary>
public interface IComputeWorker : IDisposable
{
    /// <summary>Sends a request created by <see cref="ComputeEnvelope.SerializeJob"/> and returns the worker's response.</summary>
    /// <exception cref="ComputeWorkerException">The worker failed and must not be used again.</exception>
    Task<byte[]> ExecuteAsync(byte[] request);
}

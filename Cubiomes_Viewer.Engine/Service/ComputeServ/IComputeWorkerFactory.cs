namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

public interface IComputeWorkerFactory
{
    /// <summary>The number of workers a pool should run on this device.</summary>
    int WorkerCount { get; }

    /// <exception cref="ComputeWorkerException">The worker could not be started.</exception>
    Task<IComputeWorker> StartWorkerAsync(CancellationToken cancellationToken = default);
}

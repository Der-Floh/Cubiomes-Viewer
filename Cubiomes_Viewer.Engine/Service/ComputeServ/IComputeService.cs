using Cubiomes_Viewer.Engine.Data.Compute;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

/// <summary>
/// Runs compute jobs off the caller's thread where the platform allows it. Safe to call from any thread.
/// </summary>
public interface IComputeService
{
    int WorkerCount { get; }

    /// <summary>
    /// Queues a job. A job cancelled before a worker picks it up never runs; a running job stops at its next cancellation check.
    /// </summary>
    Task<TResult> RunAsync<TResult>(ComputeJob<TResult> job, JobPriority priority, CancellationToken cancellationToken = default);
}

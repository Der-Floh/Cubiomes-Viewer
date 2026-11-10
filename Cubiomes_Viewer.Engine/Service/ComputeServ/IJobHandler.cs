using Cubiomes_Viewer.Engine.Data.Compute;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

/// <summary>
/// Executes one kind of job synchronously on a worker. Handlers are shared between workers and must keep
/// per-worker state in the <see cref="IWorkerContext"/>.
/// </summary>
public interface IJobHandler<in TJob, out TResult> where TJob : ComputeJob<TResult>
{
    TResult Handle(TJob job, IWorkerContext context, CancellationToken cancellationToken);
}

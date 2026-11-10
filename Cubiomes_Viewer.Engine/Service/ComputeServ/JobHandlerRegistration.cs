using Cubiomes_Viewer.Engine.Data.Compute;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

/// <summary>Binds a job type to the handler that executes it.</summary>
public sealed class JobHandlerRegistration
{
    public required Type JobType { get; init; }
    public required Delegate Invoker { get; init; }

    public static JobHandlerRegistration Create<TJob, TResult>(IJobHandler<TJob, TResult> handler) where TJob : ComputeJob<TResult>
    {
        ArgumentNullException.ThrowIfNull(handler);

        Func<ComputeJob<TResult>, IWorkerContext, CancellationToken, TResult> invoker =
            (job, context, cancellationToken) => handler.Handle((TJob)job, context, cancellationToken);
        return new JobHandlerRegistration { JobType = typeof(TJob), Invoker = invoker };
    }
}

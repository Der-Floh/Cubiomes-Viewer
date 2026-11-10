using Cubiomes_Viewer.Engine.Data.Compute;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

/// <summary>Binds a job type to the handler that executes it.</summary>
public sealed class JobHandlerRegistration
{
    public required Type JobType { get; init; }

    /// <summary>A <c>Func&lt;ComputeJob&lt;TResult&gt;, IWorkerContext, CancellationToken, TResult&gt;</c> for the job's result type.</summary>
    public required Delegate Invoker { get; init; }

    public required Func<ComputeJob, IWorkerContext, CancellationToken, object> UntypedInvoker { get; init; }

    public static JobHandlerRegistration Create<TJob, TResult>(IJobHandler<TJob, TResult> handler)
        where TJob : ComputeJob<TResult>
        where TResult : notnull
    {
        ArgumentNullException.ThrowIfNull(handler);

        Func<ComputeJob<TResult>, IWorkerContext, CancellationToken, TResult> invoker =
            (job, context, cancellationToken) => handler.Handle((TJob)job, context, cancellationToken);
        return new JobHandlerRegistration
        {
            JobType = typeof(TJob),
            Invoker = invoker,
            UntypedInvoker = (job, context, cancellationToken) => handler.Handle((TJob)job, context, cancellationToken),
        };
    }
}

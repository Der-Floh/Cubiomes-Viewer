using Cubiomes_Viewer.Engine.Data.Compute;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

public sealed class JobHandlerRegistry
{
    private readonly Dictionary<Type, Delegate> _invokers;

    public JobHandlerRegistry(IEnumerable<JobHandlerRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        _invokers = registrations.ToDictionary(registration => registration.JobType, registration => registration.Invoker);
    }

    public Func<ComputeJob<TResult>, IWorkerContext, CancellationToken, TResult> Resolve<TResult>(ComputeJob<TResult> job)
    {
        ArgumentNullException.ThrowIfNull(job);

        return _invokers.TryGetValue(job.GetType(), out var invoker)
            ? (Func<ComputeJob<TResult>, IWorkerContext, CancellationToken, TResult>)invoker
            : throw new InvalidOperationException($"No handler is registered for job '{job.GetType().Name}'.");
    }
}

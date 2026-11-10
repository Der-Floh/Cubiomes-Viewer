using Cubiomes_Viewer.Engine.Data.Compute;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

public sealed class JobHandlerRegistry
{
    private readonly Dictionary<Type, JobHandlerRegistration> _registrations;

    public JobHandlerRegistry(IEnumerable<JobHandlerRegistration> registrations)
    {
        ArgumentNullException.ThrowIfNull(registrations);
        _registrations = registrations.ToDictionary(registration => registration.JobType);
    }

    public Func<ComputeJob<TResult>, IWorkerContext, CancellationToken, TResult> Resolve<TResult>(ComputeJob<TResult> job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return (Func<ComputeJob<TResult>, IWorkerContext, CancellationToken, TResult>)GetRegistration(job).Invoker;
    }

    /// <summary>Executes a job whose result type is not known at compile time, as a worker host does.</summary>
    public object Invoke(ComputeJob job, IWorkerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);
        return GetRegistration(job).UntypedInvoker(job, context, cancellationToken);
    }

    private JobHandlerRegistration GetRegistration(ComputeJob job)
        => _registrations.TryGetValue(job.GetType(), out var registration)
            ? registration
            : throw new InvalidOperationException($"No handler is registered for job '{job.GetType().Name}'.");
}

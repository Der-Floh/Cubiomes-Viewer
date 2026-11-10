using Cubiomes_Viewer.Engine.Data.Compute;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

internal interface IPoolWorkItem
{
    byte[] Request { get; }
    JobPriority Priority { get; }
    bool IsCompleted { get; }

    /// <summary>Counts a hand-over to a worker and returns how many there have been.</summary>
    int BeginAttempt();

    void Complete(byte[] response);
    void Fail(Exception exception);
}

/// <summary>A serialized job waiting for, or running on, a pool worker.</summary>
internal sealed class PoolWorkItem<TResult> : IPoolWorkItem
{
    public Task<TResult> Task => _completion.Task;
    public byte[] Request { get; }
    public JobPriority Priority { get; }
    public bool IsCompleted => _completion.Task.IsCompleted;

    private readonly TaskCompletionSource<TResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenRegistration _registration;
    private int _attempts;

    public PoolWorkItem(byte[] request, JobPriority priority, CancellationToken cancellationToken)
    {
        Request = request;
        Priority = priority;
        _registration = cancellationToken.UnsafeRegister(
            static (state, token) => ((TaskCompletionSource<TResult>)state!).TrySetCanceled(token), _completion);
    }

    public int BeginAttempt() => ++_attempts;

    public void Complete(byte[] response)
    {
        try
        {
            if (!IsCompleted)
                _completion.TrySetResult(ComputeEnvelope.DeserializeResponse<TResult>(response));
        }
        catch (Exception ex)
        {
            _completion.TrySetException(ex);
        }
        finally
        {
            _registration.Dispose();
        }
    }

    public void Fail(Exception exception)
    {
        _completion.TrySetException(exception);
        _registration.Dispose();
    }
}

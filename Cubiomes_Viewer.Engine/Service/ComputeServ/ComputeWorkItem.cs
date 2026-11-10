using Cubiomes_Viewer.Engine.Data.Compute;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

internal interface IComputeWorkItem
{
    void Execute(IWorkerContext context);
}

internal sealed class ComputeWorkItem<TResult> : IComputeWorkItem
{
    public Task<TResult> Task => _completion.Task;

    private readonly ComputeJob<TResult> _job;
    private readonly Func<ComputeJob<TResult>, IWorkerContext, CancellationToken, TResult> _handler;
    private readonly CancellationToken _cancellationToken;
    private readonly TaskCompletionSource<TResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly CancellationTokenRegistration _registration;

    public ComputeWorkItem(ComputeJob<TResult> job, Func<ComputeJob<TResult>, IWorkerContext, CancellationToken, TResult> handler, CancellationToken cancellationToken)
    {
        _job = job;
        _handler = handler;
        _cancellationToken = cancellationToken;
        _registration = cancellationToken.UnsafeRegister(
            static (state, token) => ((TaskCompletionSource<TResult>)state!).TrySetCanceled(token), _completion);
    }

    public void Execute(IWorkerContext context)
    {
        try
        {
            if (_completion.Task.IsCompleted)
                return;

            _cancellationToken.ThrowIfCancellationRequested();
            _completion.TrySetResult(_handler(_job, context, _cancellationToken));
        }
        catch (OperationCanceledException)
        {
            _completion.TrySetCanceled(_cancellationToken);
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
}

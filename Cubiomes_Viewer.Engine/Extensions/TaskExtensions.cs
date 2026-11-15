namespace Cubiomes_Viewer.Engine.Extensions;

public static class TaskExtensions
{
    /// <summary>
    /// Lets go of a task that nobody waits for any more, and reads its failure should it fail, so that the failure does not
    /// surface as an unobserved task exception.
    /// </summary>
    public static void Forget(this Task task)
    {
        ArgumentNullException.ThrowIfNull(task);

        _ = task.ContinueWith(static failed => _ = failed.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
    }
}

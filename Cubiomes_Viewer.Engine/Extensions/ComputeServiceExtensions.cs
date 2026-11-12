using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

namespace Cubiomes_Viewer.Engine.Extensions;

public static class ComputeServiceExtensions
{
    extension(IComputeService compute)
    {
        /// <summary>
        /// Runs jobs with at most <paramref name="maxRunning"/> of them handed out at a time and returns their results
        /// in the order of the jobs, reporting the share done after each one.
        /// </summary>
        public async Task<TResult[]> RunAllAsync<TResult>(IReadOnlyList<ComputeJob<TResult>> jobs, JobPriority priority, int maxRunning, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(compute);
            ArgumentNullException.ThrowIfNull(jobs);
            ArgumentOutOfRangeException.ThrowIfLessThan(maxRunning, 1);

            var results = new TResult[jobs.Count];
            List<(Task<TResult> Task, int Index)> running = [];
            var next = 0;
            try
            {
                for (var done = 0; done < jobs.Count; done++)
                {
                    for (; running.Count < maxRunning && next < jobs.Count; next++)
                        running.Add((compute.RunAsync(jobs[next], priority, cancellationToken), next));

                    var finished = await Task.WhenAny(running.Select(entry => entry.Task));
                    var entry = running.Single(candidate => candidate.Task == finished);
                    running.Remove(entry);
                    results[entry.Index] = await finished;
                    progress?.Report((done + 1.0) / jobs.Count);
                }

                return results;
            }
            finally
            {
                foreach (var (task, _) in running)
                    _ = task.ContinueWith(static failed => _ = failed.Exception, CancellationToken.None, TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);
            }
        }
    }
}

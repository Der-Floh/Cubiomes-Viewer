using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

namespace Cubiomes_Viewer.Engine.Extensions;

public static class ComputeServiceExtensions
{
    extension(IComputeService compute)
    {
        /// <summary>
        /// Runs jobs with at most <paramref name="maxRunning"/> of them handed out at a time and returns their results
        /// in the order of the jobs, reporting the share done after each one. When one fails, the others still running
        /// are cancelled.
        /// </summary>
        public async Task<TResult[]> RunAllAsync<TResult>(IReadOnlyList<ComputeJob<TResult>> jobs, JobPriority priority, int maxRunning, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(compute);
            ArgumentNullException.ThrowIfNull(jobs);
            ArgumentOutOfRangeException.ThrowIfLessThan(maxRunning, 1);

            var results = new TResult[jobs.Count];
            List<(Task<TResult> Task, int Index)> running = [];
            using var stopping = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var next = 0;
            try
            {
                for (var done = 0; done < jobs.Count; done++)
                {
                    for (; running.Count < maxRunning && next < jobs.Count; next++)
                        running.Add((compute.RunAsync(jobs[next], priority, stopping.Token), next));

                    var finished = await Task.WhenAny(running.Select(entry => entry.Task)).ConfigureAwait(false);
                    var entry = running.Single(candidate => candidate.Task == finished);
                    running.Remove(entry);
                    results[entry.Index] = await finished.ConfigureAwait(false);
                    progress?.Report((done + 1.0) / jobs.Count);
                }

                return results;
            }
            finally
            {
                stopping.Cancel();
                foreach (var (task, _) in running)
                    task.Forget();
            }
        }
    }
}

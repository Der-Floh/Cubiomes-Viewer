namespace Cubiomes_Viewer.Engine.Data.Compute;

/// <summary>
/// A small, self-contained unit of work. Jobs carry everything a worker needs, so any worker
/// (a thread, or a Web Worker in the browser) can execute any job.
/// </summary>
public abstract record ComputeJob;

public abstract record ComputeJob<TResult> : ComputeJob;

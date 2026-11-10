namespace Cubiomes_Viewer.Engine.Data.Compute;

/// <summary>A job failure as it travels from a worker in another runtime back to the caller.</summary>
public sealed class WorkerError
{
    public required string Message { get; init; }

    /// <summary>The native status when the failure came from the generator, otherwise <see langword="null"/>.</summary>
    public int? NativeStatus { get; init; }
}

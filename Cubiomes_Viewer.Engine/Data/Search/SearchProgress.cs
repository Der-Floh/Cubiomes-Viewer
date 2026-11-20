namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>How far a search has come.</summary>
/// <param name="Scanned">The seeds looked at so far; they are the first ones of the source, which can hold 2^64 of them.</param>
/// <param name="NextSeed">The seed the search continues with, or <see langword="null"/> when there is none or it is not known yet.</param>
/// <param name="SeedsPerSecond">The recent speed, or zero while it is unknown.</param>
/// <param name="Remaining">The time until the last seed at the recent speed, when the source has an end and the search a speed.</param>
/// <param name="Preparing">The share of the seeds the source has prepared, from 0 to 1, while it prepares them before the first scan.</param>
/// <param name="Total">The seeds of the source once it is prepared, when it has an end; <see langword="null"/> for a source that runs to the last seed.</param>
public readonly record struct SearchProgress(UInt128 Scanned, long? NextSeed, double SeedsPerSecond, TimeSpan? Remaining, double? Preparing = null, UInt128? Total = null)
{
    /// <summary>
    /// How far the search has come, from 0 to 1: the share prepared while the source prepares, then the share scanned of a
    /// source with an end; <see langword="null"/> when there is no telling.
    /// </summary>
    public double? Share => Preparing ?? (Total is { } total && total > 0 ? Math.Min(1, (double)Scanned / (double)total) : null);
}

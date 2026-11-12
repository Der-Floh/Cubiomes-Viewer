namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>How far a search has come.</summary>
/// <param name="Scanned">The seeds looked at so far; they are the first ones of the source.</param>
/// <param name="NextSeed">The seed the search continues with, or <see langword="null"/> when there is none or it is not known yet.</param>
/// <param name="SeedsPerSecond">The recent speed, or zero while it is unknown.</param>
/// <param name="Remaining">The time until the last seed at the recent speed, when the source has an end and the search a speed.</param>
/// <param name="Preparing">The share of the seeds the source has prepared, from 0 to 1, while it prepares them before the first scan.</param>
public readonly record struct SearchProgress(long Scanned, long? NextSeed, double SeedsPerSecond, TimeSpan? Remaining, double? Preparing = null);

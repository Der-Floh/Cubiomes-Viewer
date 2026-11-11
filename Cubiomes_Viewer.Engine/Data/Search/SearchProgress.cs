namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>How far a search has come.</summary>
/// <param name="Scanned">The seeds looked at so far.</param>
/// <param name="NextSeed">The seed the search continues with; every seed before it has been looked at.</param>
/// <param name="SeedsPerSecond">The recent speed, or zero while it is unknown.</param>
/// <param name="Remaining">The time until the last seed at the recent speed, when the search has an end and a speed.</param>
public readonly record struct SearchProgress(long Scanned, long NextSeed, double SeedsPerSecond, TimeSpan? Remaining);

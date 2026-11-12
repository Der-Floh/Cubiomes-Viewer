using System.Globalization;

using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Data;

/// <summary>Says in words where a search stands and how far it has come.</summary>
public static class SearchProgressText
{
    private static readonly TimeSpan _minute = TimeSpan.FromMinutes(1);
    private static readonly TimeSpan _hour = TimeSpan.FromHours(1);
    private static readonly TimeSpan _twoDays = TimeSpan.FromDays(2);
    private static readonly TimeSpan _year = TimeSpan.FromDays(365);

    /// <param name="state">Where the search stands.</param>
    /// <param name="progress">How far it has come.</param>
    /// <param name="found">How many seeds it found.</param>
    /// <param name="maxResults">How many seeds it stops at.</param>
    /// <param name="canContinue">Whether a stopped search can continue with the setup as it is now.</param>
    public static string DescribeState(SearchState state, SearchProgress progress, int found, int maxResults, bool canContinue) => state switch
    {
        SearchState.Idle => string.Empty,
        SearchState.Running when progress.Preparing is not null => "Preparing the seeds to scan…",
        SearchState.Running => "Searching…",
        SearchState.Paused when canContinue && progress.NextSeed is { } next => string.Create(CultureInfo.InvariantCulture, $"Stopped before seed {next}."),
        SearchState.Paused when canContinue => "Stopped.",
        SearchState.Paused => "Stopped. The setup has changed since, so a new search starts over.",
        SearchState.Finished when found >= maxResults => $"Finished after finding {Count(found, "seed")}.",
        SearchState.Finished => "Finished: every seed was scanned.",
        _ => throw new NotSupportedException($"Unsupported search state: {state}"),
    };

    /// <summary>The seeds scanned and, while the search runs, its speed and the time it has left.</summary>
    public static string DescribeProgress(SearchState state, SearchProgress progress)
    {
        if (state == SearchState.Idle)
            return string.Empty;
        if (state == SearchState.Running && progress.Preparing is { } share)
            return string.Create(CultureInfo.InvariantCulture, $"{share * 100:0} % prepared · this happens once, the result is kept");

        List<string> parts = [$"{Count(progress.Scanned, "seed")} scanned"];
        if (state == SearchState.Running && progress.SeedsPerSecond > 0)
            parts.Add($"{FormatNumber(progress.SeedsPerSecond)} per second");
        if (state == SearchState.Running && progress.Remaining is { } remaining)
            parts.Add(DescribeRemaining(remaining));
        return string.Join(" · ", parts);
    }

    public static string DescribeRemaining(TimeSpan remaining)
    {
        if (remaining < _minute)
            return "less than a minute left";
        if (remaining < _hour)
            return $"about {Count(Math.Round(remaining.TotalMinutes), "minute")} left";
        if (remaining < _twoDays)
            return $"about {Count(Math.Round(remaining.TotalHours), "hour")} left";
        return remaining < _year ? $"about {Count(Math.Round(remaining.TotalDays), "day")} left" : "more than a year left";
    }

    /// <summary>A number with the noun that counts it, such as "1 seed" or "2,500 seeds".</summary>
    public static string Count(double count, string noun) => count == 1 ? $"1 {noun}" : $"{FormatNumber(count)} {noun}s";

    private static string FormatNumber(double value) => value.ToString("N0", CultureInfo.InvariantCulture);
}

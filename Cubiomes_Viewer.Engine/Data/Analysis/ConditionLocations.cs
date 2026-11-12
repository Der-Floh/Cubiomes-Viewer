using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Analysis;

/// <summary>
/// Where the conditions of a search hold for one seed. For a seed that meets them, the conditions relative to another
/// one list what they found around the place that made the seed match.
/// </summary>
public sealed class ConditionLocations
{
    public required long Seed { get; init; }

    public required bool IsMatch { get; init; }

    /// <summary>The places each condition found, condition by condition in the order of the definition.</summary>
    public required ConditionPlace[] Places { get; init; }
}

public readonly record struct ConditionPlace(int ConditionId, BlockPos Position);

using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Structures;

/// <summary>The fixed points of a world's Overworld: where players spawn and where the strongholds are.</summary>
public sealed class WorldLandmarks
{
    public required WorldKey World { get; init; }

    /// <summary><see langword="null"/> for versions whose spawn point cannot be predicted.</summary>
    public BlockPos? Spawn { get; init; }

    /// <summary>All strongholds in generation order.</summary>
    public required BlockPos[] Strongholds { get; init; }
}

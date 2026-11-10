using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Compute;

/// <summary>Finds the spawn point and the strongholds of a world; the dimension of <paramref name="World"/> is ignored.</summary>
public sealed record LandmarksJob(WorldKey World) : ComputeJob<WorldLandmarks>;

using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

/// <summary>State owned by a single worker, such as its native generators.</summary>
public interface IWorkerContext
{
    /// <summary>Returns this worker's generator for the world, already seeded for its dimension and seed.</summary>
    IWorldGenerator GetGenerator(WorldKey world);
}

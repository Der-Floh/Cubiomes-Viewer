using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

/// <summary>Finds the spawn point and the strongholds of the world a map shows. The dimension of a world key is ignored.</summary>
public interface IWorldLandmarkService
{
    /// <summary>Raised when landmarks became available. May fire on a worker thread.</summary>
    event Action? StateChanged;

    /// <summary>Declares the world whose landmarks are needed; they are searched unless known, and a search for another world is cancelled.</summary>
    void Show(WorldKey world);

    bool TryGetLandmarks(WorldKey world, [NotNullWhen(true)] out WorldLandmarks? landmarks);
}

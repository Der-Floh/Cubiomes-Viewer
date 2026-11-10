using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Structures;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

/// <summary>Finds the slime chunks a map view needs area by area, keeps them in a cache and drops work that is no longer visible.</summary>
public interface ISlimeAreaService
{
    /// <summary>Raised when slime chunks became available. May fire on a worker thread.</summary>
    event Action? StateChanged;

    /// <summary>Lists the areas of chunks that intersect the viewport, nearest to the view centre first.</summary>
    IReadOnlyList<SlimeAreaRequest> GetVisibleAreas(long seed, MapViewport viewport);

    /// <summary>
    /// Declares the areas currently needed, most important first. Missing areas are computed in that order;
    /// pending areas that are no longer listed are cancelled.
    /// </summary>
    void SetVisibleAreas(IReadOnlyList<SlimeAreaRequest> requests);

    bool TryGetSlimeChunks(SlimeAreaRequest request, [NotNullWhen(true)] out SlimeAreaData? slimeChunks);
}

using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

/// <summary>Finds the structures a map view needs area by area, keeps them in a cache and drops work that is no longer visible.</summary>
public interface IStructureAreaService
{
    /// <summary>Raised when structures became available or the number of pending areas changed. May fire on a worker thread.</summary>
    event Action? StateChanged;

    int PendingCount { get; }

    /// <summary>
    /// Lists the search areas of a structure type that intersect the viewport, nearest to the view centre first.
    /// Returns nothing when the type does not generate in the dimension and version of <paramref name="world"/>.
    /// </summary>
    IReadOnlyList<StructureAreaRequest> GetVisibleAreas(WorldKey world, StructureType type, MapViewport viewport);

    /// <summary>
    /// Declares the areas currently needed, most important first. Missing areas are searched in that order;
    /// pending areas that are no longer listed are cancelled.
    /// </summary>
    void SetVisibleAreas(IReadOnlyList<StructureAreaRequest> requests);

    bool TryGetStructures(StructureAreaRequest request, [NotNullWhen(true)] out StructureAreaData? structures);
}

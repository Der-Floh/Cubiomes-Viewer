using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

/// <summary>Finds the End gateways of the world a map shows and where they lead. The dimension of a world key is ignored.</summary>
public interface IEndGatewayService
{
    /// <summary>Raised when gateways became available. May fire on a worker thread.</summary>
    event Action? StateChanged;

    /// <summary>Declares the world whose gateways are needed; they are searched unless known, and a search for another world is cancelled.</summary>
    void Show(WorldKey world);

    bool TryGetGateways(WorldKey world, [NotNullWhen(true)] out EndGatewayData? gateways);
}

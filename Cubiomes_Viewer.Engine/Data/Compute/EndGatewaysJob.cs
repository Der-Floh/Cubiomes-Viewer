using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Compute;

/// <summary>Finds the gateways around the main End island and where they lead; the dimension of <paramref name="World"/> is ignored.</summary>
public sealed record EndGatewaysJob(WorldKey World) : ComputeJob<EndGatewayData>;

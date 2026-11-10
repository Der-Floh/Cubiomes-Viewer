using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Structures;

public sealed class EndGatewayData
{
    public required WorldKey World { get; init; }

    /// <summary>The gateways in the order they appear; empty for versions whose gateways cannot be followed.</summary>
    public required EndGateway[] Gateways { get; init; }
}

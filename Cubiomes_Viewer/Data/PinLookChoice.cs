using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Data;

/// <summary>A look a pin can be given, with the name it is offered by.</summary>
public sealed class PinLookChoice
{
    public required string Display { get; init; }
    public required PinLook Look { get; init; }

    public override string ToString() => Display;
}

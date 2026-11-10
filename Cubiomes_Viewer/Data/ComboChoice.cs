namespace Cubiomes_Viewer.Data;

public sealed class ComboChoice<T>
{
    public required string Display { get; init; }
    public required T Value { get; init; }

    public override string ToString() => Display;
}

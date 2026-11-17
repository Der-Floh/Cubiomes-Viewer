namespace Cubiomes_Viewer.Engine.Data.Structures;

/// <summary>
/// The height a structure lies at: one level, or the levels it lies between where the game settles the height by chance or by
/// terrain the generator does not build. An estimate follows the approximate surface the structure stands on.
/// </summary>
public sealed record StructureHeight(int Lowest, int Highest, bool IsEstimate = false)
{
    public bool IsRange => Lowest != Highest;

    public static StructureHeight At(int y) => new(y, y);

    public static StructureHeight Around(int y) => new(y, y, IsEstimate: true);

    public static StructureHeight Between(int lowest, int highest)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(highest, lowest);

        return new StructureHeight(lowest, highest);
    }
}

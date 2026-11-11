namespace Cubiomes_Viewer.Engine.Data.Colors;

/// <summary>
/// Turns a number into a colour by blending the two stops around it. Numbers outside the ramp take the colour of the nearest end.
/// </summary>
public sealed class ColorRamp
{
    private const int MinStops = 2;

    public IReadOnlyList<ColorStop> Stops => _stops;
    public double Minimum => _stops[0].Position;
    public double Maximum => _stops[^1].Position;

    private readonly ColorStop[] _stops;

    public ColorRamp(params ColorStop[] stops)
    {
        ArgumentNullException.ThrowIfNull(stops);
        if (stops.Length < MinStops)
            throw new ArgumentException($"A colour ramp needs at least {MinStops} stops but {stops.Length} were given.", nameof(stops));
        if (!stops.Zip(stops.Skip(1)).All(pair => pair.First.Position < pair.Second.Position))
            throw new ArgumentException("The stops of a colour ramp must be in ascending order of their positions.", nameof(stops));

        _stops = [.. stops];
    }

    public RgbColor GetColor(double position)
    {
        if (position <= Minimum)
            return _stops[0].Color;
        if (position >= Maximum)
            return _stops[^1].Color;

        var upper = Array.FindIndex(_stops, stop => stop.Position > position);
        var (from, to) = (_stops[upper - 1], _stops[upper]);
        return Blend(from.Color, to.Color, (position - from.Position) / (to.Position - from.Position));
    }

    private static RgbColor Blend(RgbColor from, RgbColor to, double amount)
        => new(Blend(from.Red, to.Red, amount), Blend(from.Green, to.Green, amount), Blend(from.Blue, to.Blue, amount));

    private static byte Blend(byte from, byte to, double amount) => (byte)Math.Round(from + (to - from) * amount);
}

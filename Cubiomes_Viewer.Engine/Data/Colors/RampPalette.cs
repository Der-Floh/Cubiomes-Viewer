namespace Cubiomes_Viewer.Engine.Data.Colors;

/// <summary>
/// A colour ramp cut into steps of equal colour change for the whole numbers a layer stores, <c>unit</c> numbers to one
/// unit of the ramp. Each number takes the colour of the step its value falls in, so a layer painted with the palette has
/// no more colours than steps and an image of it fits a PNG palette. The steps run in ramp order, from the lowest value up.
/// </summary>
public sealed class RampPalette
{
    public const int MaxSteps = byte.MaxValue + 1;

    private const int StoredNumberCount = ushort.MaxValue + 1;

    private readonly byte[] _steps = new byte[StoredNumberCount];

    private RampPalette(RgbColor[] colors) => Colors = colors;

    public IReadOnlyList<RgbColor> Colors { get; }

    /// <summary>The step a stored number falls in; numbers beyond the ends of the ramp take the first or the last step.</summary>
    public byte GetStep(short stored) => _steps[(ushort)stored];

    public RgbColor GetColor(short stored) => Colors[GetStep(stored)];

    /// <summary>Cuts a ramp into steps, each the colour of the middle number of the numbers that fall in it.</summary>
    public static RampPalette Create(ColorRamp ramp, int unit, int steps)
    {
        ArgumentNullException.ThrowIfNull(ramp);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(unit);
        ArgumentOutOfRangeException.ThrowIfLessThan(steps, 2);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(steps, MaxSteps);

        var first = (int)Math.Clamp(Math.Floor(ramp.Minimum * unit), short.MinValue, short.MaxValue);
        var last = (int)Math.Clamp(Math.Ceiling(ramp.Maximum * unit), short.MinValue, short.MaxValue);
        var colors = new RgbColor[last - first + 1];
        var change = new double[colors.Length];
        for (var index = 0; index < colors.Length; index++)
        {
            colors[index] = ramp.GetColor((first + index) / (double)unit);
            change[index] = index == 0 ? 0 : change[index - 1] + GetDistance(colors[index - 1], colors[index]);
        }

        var stepOf = new byte[colors.Length];
        for (var index = 0; index < colors.Length; index++)
            stepOf[index] = (byte)(change[^1] == 0 ? 0 : Math.Min(steps - 1, (int)(change[index] / change[^1] * steps)));

        var palette = new RampPalette(GetStepColors(colors, stepOf, steps));
        for (int stored = short.MinValue; stored <= short.MaxValue; stored++)
            palette._steps[(ushort)stored] = stepOf[Math.Clamp(stored - first, 0, colors.Length - 1)];
        return palette;
    }

    /// <summary>
    /// The colour of the middle number of each step, but the very ends of the ramp for the first and the last step, which the
    /// numbers beyond them take too; a step no number falls in repeats the step before it.
    /// </summary>
    private static RgbColor[] GetStepColors(RgbColor[] colors, byte[] stepOf, int steps)
    {
        var (lowest, highest) = (Enumerable.Repeat(-1, steps).ToArray(), new int[steps]);
        for (var index = 0; index < colors.Length; index++)
        {
            if (lowest[stepOf[index]] < 0)
                lowest[stepOf[index]] = index;
            highest[stepOf[index]] = index;
        }

        var stepColors = new RgbColor[steps];
        for (var step = 0; step < steps; step++)
            stepColors[step] = lowest[step] >= 0 ? colors[(lowest[step] + highest[step]) / 2] : stepColors[step - 1];
        stepColors[0] = colors[0];
        if (lowest[^1] >= 0)
            stepColors[^1] = colors[^1];
        return stepColors;
    }

    private static double GetDistance(RgbColor from, RgbColor to)
    {
        var (red, green, blue) = (to.Red - from.Red, to.Green - from.Green, to.Blue - from.Blue);
        return Math.Sqrt(red * red + green * green + blue * blue);
    }
}

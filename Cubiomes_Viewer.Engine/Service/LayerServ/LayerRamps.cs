using Cubiomes_Viewer.Engine.Data.Colors;

namespace Cubiomes_Viewer.Engine.Service.LayerServ;

/// <summary>
/// The colours of the layers of numbers. The stops of the climate ramps sit where the biome lookup of the
/// noise-based Overworld draws its lines, so a change of colour is a change the generator reacts to.
/// </summary>
internal static class LayerRamps
{
    private static readonly RgbColor _coldest = new(22, 42, 160);
    private static readonly RgbColor _cold = new(64, 142, 230);
    private static readonly RgbColor _cool = new(134, 212, 240);
    private static readonly RgbColor _mild = new(250, 240, 170);
    private static readonly RgbColor _warm = new(245, 150, 60);
    private static readonly RgbColor _hottest = new(190, 30, 30);

    private static readonly RgbColor _driest = new(150, 100, 50);
    private static readonly RgbColor _dry = new(215, 185, 110);
    private static readonly RgbColor _moderate = new(235, 230, 160);
    private static readonly RgbColor _moist = new(150, 205, 110);
    private static readonly RgbColor _wet = new(50, 150, 90);
    private static readonly RgbColor _wettest = new(10, 80, 110);

    public static ColorRamp Temperature { get; } = new(
        new(-1.0, _coldest), new(-0.45, _cold), new(-0.15, _cool), new(0.2, _mild), new(0.55, _warm), new(1.0, _hottest));

    public static ColorRamp Humidity { get; } = new(
        new(-1.0, _driest), new(-0.35, _dry), new(-0.1, _moderate), new(0.1, _moist), new(0.3, _wet), new(1.0, _wettest));

    public static ColorRamp Continentalness { get; } = new(
        new(-1.2, new(180, 60, 190)),
        new(-1.05, new(10, 20, 90)),
        new(-0.455, new(30, 70, 170)),
        new(-0.19, new(90, 150, 220)),
        new(-0.11, new(235, 220, 160)),
        new(0.03, new(140, 195, 100)),
        new(0.3, new(60, 130, 60)),
        new(1.0, new(90, 70, 50)));

    public static ColorRamp Erosion { get; } = new(
        new(-1.0, new(70, 30, 100)),
        new(-0.78, new(120, 60, 140)),
        new(-0.375, new(190, 110, 120)),
        new(-0.2225, new(230, 170, 110)),
        new(0.05, new(240, 220, 150)),
        new(0.45, new(170, 205, 140)),
        new(0.55, new(120, 175, 135)),
        new(1.0, new(60, 110, 120)));

    public static ColorRamp Depth { get; } = new(
        new(-2.0, new(250, 250, 215)),
        new(-0.5, new(170, 205, 235)),
        new(0.0, new(120, 170, 90)),
        new(0.5, new(140, 105, 70)),
        new(1.5, new(45, 35, 30)));

    public static ColorRamp Weirdness { get; } = new(
        new(-1.0, new(40, 90, 170)), new(0.0, new(240, 240, 240)), new(1.0, new(190, 70, 50)));

    /// <summary>Beta 1.7 measures temperature and humidity from 0 to 1.</summary>
    public static ColorRamp BetaTemperature { get; } = new(
        new(0.0, _coldest), new(0.2, _cold), new(0.4, _cool), new(0.6, _mild), new(0.8, _warm), new(1.0, _hottest));

    public static ColorRamp BetaHumidity { get; } = new(
        new(0.0, _driest), new(0.2, _dry), new(0.4, _moderate), new(0.6, _moist), new(0.8, _wet), new(1.0, _wettest));

    /// <summary>Blues below the sea level of 63 blocks, land colours above.</summary>
    public static ColorRamp OverworldHeight { get; } = new(
        new(-64, new(5, 10, 50)),
        new(0, new(10, 30, 100)),
        new(40, new(30, 80, 170)),
        new(62, new(110, 180, 230)),
        new(63, new(230, 220, 170)),
        new(66, new(125, 190, 95)),
        new(90, new(60, 140, 65)),
        new(120, new(40, 105, 55)),
        new(150, new(150, 140, 85)),
        new(190, new(130, 100, 70)),
        new(240, new(205, 205, 205)),
        new(320, new(255, 255, 255)));

    /// <summary>Most of the range goes to the heights where most of the land lies.</summary>
    public static ColorRamp OverworldHeightGray { get; } = new(
        new(-64, new(0, 0, 0)), new(63, new(90, 90, 90)), new(140, new(200, 200, 200)), new(320, new(255, 255, 255)));

    public static ColorRamp EndHeightGray { get; } = new(
        new(0, new(0, 0, 0)), new(1, new(70, 70, 70)), new(90, new(255, 255, 255)));

    /// <summary>The void has a height of zero; the islands rise to about 70 blocks.</summary>
    public static ColorRamp EndHeight { get; } = new(
        new(0, new(15, 10, 25)),
        new(1, new(60, 40, 90)),
        new(40, new(150, 130, 170)),
        new(60, new(220, 215, 170)),
        new(90, new(255, 250, 220)));
}

using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Service.LayerServ;

namespace Cubiomes_Viewer.Engine.Tests.Data;

public sealed class RampPaletteTests
{
    public static TheoryData<string, int> RampsOfLayers() => new()
    {
        { nameof(LayerRamps.Temperature), Constants.ClimateUnit },
        { nameof(LayerRamps.Humidity), Constants.ClimateUnit },
        { nameof(LayerRamps.Continentalness), Constants.ClimateUnit },
        { nameof(LayerRamps.Erosion), Constants.ClimateUnit },
        { nameof(LayerRamps.Depth), Constants.ClimateUnit },
        { nameof(LayerRamps.Weirdness), Constants.ClimateUnit },
        { nameof(LayerRamps.BetaTemperature), Constants.ClimateUnit },
        { nameof(LayerRamps.BetaHumidity), Constants.ClimateUnit },
        { nameof(LayerRamps.OverworldHeight), Constants.HeightUnit },
        { nameof(LayerRamps.EndHeight), Constants.HeightUnit },
    };

    [Theory]
    [MemberData(nameof(RampsOfLayers))]
    public void Create_RampOfALayer_StaysWithinAFewShadesOfIt(string name, int unit)
    {
        const int MaxChannelDifference = 4;
        var ramp = GetRamp(name);

        var palette = RampPalette.Create(ramp, unit, RampPalette.MaxSteps);

        for (int stored = short.MinValue; stored <= short.MaxValue; stored++)
        {
            var (stepped, exact) = (palette.GetColor((short)stored), ramp.GetColor(stored / (double)unit));
            Assert.True(Math.Abs(stepped.Red - exact.Red) <= MaxChannelDifference && Math.Abs(stepped.Green - exact.Green) <= MaxChannelDifference
                && Math.Abs(stepped.Blue - exact.Blue) <= MaxChannelDifference, $"{name} at {stored}: {stepped} instead of about {exact}.");
        }
    }

    [Theory]
    [MemberData(nameof(RampsOfLayers))]
    public void Create_RampOfALayer_RunsItsStepsInRampOrder(string name, int unit)
    {
        var palette = RampPalette.Create(GetRamp(name), unit, RampPalette.MaxSteps);

        var steps = Enumerable.Range(short.MinValue, ushort.MaxValue + 1).Select(stored => palette.GetStep((short)stored)).ToArray();

        Assert.Equal(steps.Order(), steps);
        Assert.Equal((0, RampPalette.MaxSteps - 1), ((int)steps[0], (int)steps[^1]));
    }

    [Fact]
    public void Create_Ends_TakeTheColoursOfTheEndsOfTheRamp()
    {
        var ramp = new ColorRamp(new(-1, new RgbColor(0, 0, 0)), new(1, new RgbColor(250, 100, 20)));

        var palette = RampPalette.Create(ramp, 100, 16);

        Assert.Equal(16, palette.Colors.Count);
        Assert.Equal((ramp.Stops[0].Color, ramp.Stops[^1].Color), (palette.GetColor(-100), palette.GetColor(100)));
        Assert.Equal((ramp.Stops[0].Color, ramp.Stops[^1].Color), (palette.GetColor(short.MinValue), palette.GetColor(short.MaxValue)));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(257)]
    public void Create_StepsAPaletteCannotHold_AreRefused(int steps)
        => Assert.Throws<ArgumentOutOfRangeException>(() => RampPalette.Create(LayerRamps.Temperature, Constants.ClimateUnit, steps));

    private static ColorRamp GetRamp(string name)
        => (ColorRamp)typeof(LayerRamps).GetProperty(name)!.GetValue(null)!;
}

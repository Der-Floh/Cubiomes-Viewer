using System.Globalization;

using Avalonia.Headless.XUnit;
using Avalonia.Media;

using Cubiomes_Viewer.Converters;
using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class LegendViewModelTests : IDisposable
{
    private readonly ServiceProvider _services = TestServices.Create();
    private readonly LegendViewModel _legend;
    private readonly WorldSession _session;

    public LegendViewModelTests()
    {
        _session = _services.GetRequiredService<WorldSession>();
        _legend = _services.GetRequiredService<LegendViewModel>();
    }

    public void Dispose() => _services.Dispose();

    [AvaloniaFact]
    public void Layer_Biomes_NeedsNoLegend()
        => Assert.False(_legend.IsVisible);

    [AvaloniaFact]
    public void Layer_ClimateParameter_ShowsItsRangeWithTwoDecimals()
    {
        _session.Layer = LayerMode.Temperature;

        Assert.True(_legend.IsVisible);
        Assert.Equal("Temperature", _legend.Title);
        Assert.Equal("-1.00", _legend.MinimumText);
        Assert.Equal("1.00", _legend.MaximumText);
    }

    [AvaloniaFact]
    public void Layer_Height_ShowsBlocksAndFollowsTheDimension()
    {
        _session.Layer = LayerMode.Height;
        Assert.Equal(("Surface height", "-64", "320"), (_legend.Title, _legend.MinimumText, _legend.MaximumText));

        _session.Dimension = Dimension.End;
        Assert.Equal(("0", "90"), (_legend.MinimumText, _legend.MaximumText));

        _session.Dimension = Dimension.Nether;
        Assert.False(_legend.IsVisible);
    }

    [AvaloniaFact]
    public void Ramp_Converted_BecomesAGradientThroughItsStops()
    {
        _session.Layer = LayerMode.Weirdness;
        var ramp = Assert.IsType<ColorRamp>(_legend.Ramp);

        var brush = Assert.IsType<LinearGradientBrush>(ColorRampBrushConverter.Instance.Convert(ramp, typeof(IBrush), null, CultureInfo.InvariantCulture));

        Assert.Equal([0, 0.5, 1], brush.GradientStops.Select(stop => stop.Offset));
        Assert.Equal(ramp.Stops.Select(stop => Color.FromRgb(stop.Color.Red, stop.Color.Green, stop.Color.Blue)), brush.GradientStops.Select(stop => stop.Color));
        Assert.Null(ColorRampBrushConverter.Instance.Convert(null, typeof(IBrush), null, CultureInfo.InvariantCulture));
    }
}

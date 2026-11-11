using System.Globalization;

using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

using Cubiomes_Viewer.Engine.Data.Colors;

namespace Cubiomes_Viewer.Converters;

/// <summary>Turns a colour ramp into a brush that runs through its colours from left to right.</summary>
public sealed class ColorRampBrushConverter : IValueConverter
{
    public static ColorRampBrushConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (value is not ColorRamp ramp)
            return null;

        var brush = new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0.5, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 0.5, RelativeUnit.Relative),
        };
        foreach (var stop in ramp.Stops)
        {
            var offset = (stop.Position - ramp.Minimum) / (ramp.Maximum - ramp.Minimum);
            brush.GradientStops.Add(new GradientStop(Color.FromRgb(stop.Color.Red, stop.Color.Green, stop.Color.Blue), offset));
        }

        return brush;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("A brush cannot be turned back into a colour ramp.");
}

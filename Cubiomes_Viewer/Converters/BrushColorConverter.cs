using System.Globalization;

using Avalonia.Data.Converters;
using Avalonia.Media;

namespace Cubiomes_Viewer.Converters;

/// <summary>Turns a solid brush into its colour, so that an SVG icon can be drawn in the foreground of the control around it.</summary>
public sealed class BrushColorConverter : IValueConverter
{
    public static BrushColorConverter Instance { get; } = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        => value is ISolidColorBrush brush ? brush.Color : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("A colour is not turned back into a brush.");
}

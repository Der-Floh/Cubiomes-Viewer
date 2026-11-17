using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Svg.Skia;

namespace Cubiomes_Viewer.Controls;

/// <summary>
/// Draws an icon whose shapes fill with currentColor in the foreground of the control around it, as a PathIcon draws a
/// geometry: its outlined form, or its filled form while <see cref="IsFilled"/> is set. Unlike the Svg control it has
/// its images at once, so no icon appears late; they come from <see cref="SvgImageCache"/>, which parses each file
/// once per colour for all icons.
/// </summary>
public sealed class SvgIcon : Control
{
    public static readonly StyledProperty<SvgIconSource?> SourceProperty = AvaloniaProperty.Register<SvgIcon, SvgIconSource?>(nameof(Source));

    public static readonly StyledProperty<bool> IsFilledProperty = AvaloniaProperty.Register<SvgIcon, bool>(nameof(IsFilled));

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<SvgIcon>();

    private SvgImage? _outlined;
    private SvgImage? _filled;

    static SvgIcon() => AffectsRender<SvgIcon>(SourceProperty, IsFilledProperty, ForegroundProperty);

    public SvgIconSource? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    /// <summary>Whether the filled form is shown, as it is while the control around the icon is checked, pressed or open.</summary>
    public bool IsFilled
    {
        get => GetValue(IsFilledProperty);
        set => SetValue(IsFilledProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    private SvgImage? Shown => IsFilled ? _filled ?? _outlined : _outlined;

    public override void Render(DrawingContext context)
    {
        if (Shown is not { Size: { Width: > 0, Height: > 0 } size } image)
            return;

        var scale = Math.Min(Bounds.Width / size.Width, Bounds.Height / size.Height);
        var drawn = size * scale;
        context.DrawImage(image, new Rect(new Point((Bounds.Width - drawn.Width) / 2, (Bounds.Height - drawn.Height) / 2), drawn));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SourceProperty || change.Property == ForegroundProperty)
        {
            var color = (Foreground as ISolidColorBrush)?.Color;
            _outlined = GetImage(Source?.Outlined, color);
            _filled = GetImage(Source?.Filled, color);
        }
    }

    private static SvgImage? GetImage(string? path, Color? color) => string.IsNullOrEmpty(path) ? null : SvgImageCache.Get(path, color);
}

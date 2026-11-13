using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using Avalonia.Svg.Skia;

namespace Cubiomes_Viewer.Controls;

/// <summary>
/// Draws an SVG file whose shapes fill with currentColor in the foreground of the control around it, as a PathIcon
/// draws a geometry. Unlike the Svg control it loads the file at once on the UI thread, so no icon appears late.
/// </summary>
public sealed class SvgIcon : Control
{
    public static readonly StyledProperty<string?> PathProperty = AvaloniaProperty.Register<SvgIcon, string?>(nameof(Path));

    public static readonly StyledProperty<IBrush?> ForegroundProperty = TextElement.ForegroundProperty.AddOwner<SvgIcon>();

    private static readonly Uri _assetBaseUri = new($"avares://{typeof(SvgIcon).Assembly.GetName().Name}/");

    private SvgImage? _image;

    static SvgIcon() => AffectsRender<SvgIcon>(PathProperty, ForegroundProperty);

    /// <summary>The asset path of the SVG file, such as /Assets/Icons/home.svg.</summary>
    public string? Path
    {
        get => GetValue(PathProperty);
        set => SetValue(PathProperty, value);
    }

    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    public override void Render(DrawingContext context)
    {
        if (_image is not { Size: { Width: > 0, Height: > 0 } size } image)
            return;

        var scale = Math.Min(Bounds.Width / size.Width, Bounds.Height / size.Height);
        var drawn = size * scale;
        context.DrawImage(image, new Rect(new Point((Bounds.Width - drawn.Width) / 2, (Bounds.Height - drawn.Height) / 2), drawn));
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == PathProperty)
            _image = Path is { } path ? new SvgImage { Source = SvgSource.Load(path, _assetBaseUri) } : null;
        if ((change.Property == PathProperty || change.Property == ForegroundProperty) && _image is not null)
            _image.CurrentColor = (Foreground as ISolidColorBrush)?.Color;
    }
}

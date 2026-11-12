using System.Globalization;

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>Draws a coordinate grid whose spacing grows from <see cref="BaseSpacing"/> by <see cref="Multiplier"/> until lines are comfortably apart.</summary>
public sealed class GridLayer : IMapLayer
{
    private const double MinSpacingPixels = 96;
    private const double LabelFontSize = 11;
    private const double LabelMargin = 4;
    private const double LineOpacity = 0.3;
    private const double AxisOpacity = 0.65;
    private const double AxisThickness = 2;

    private static readonly IPen _linePen = new ImmutablePen(new ImmutableSolidColorBrush(Colors.Black, LineOpacity));
    private static readonly IPen _axisPen = new ImmutablePen(new ImmutableSolidColorBrush(Colors.Black, AxisOpacity), AxisThickness);
    private static readonly IBrush _labelBrush = new ImmutableSolidColorBrush(Colors.White);
    private static readonly IBrush _labelShadowBrush = new ImmutableSolidColorBrush(Colors.Black, AxisOpacity);
    private static readonly Vector _shadowOffset = new(1, 1);

    public event EventHandler? Invalidated
    {
        add { }
        remove { }
    }

    public bool IsVisible { get; set; } = true;

    /// <summary>The blocks between lines when zoomed in far enough.</summary>
    public long BaseSpacing { get; set; } = 16;

    /// <summary>The factor the spacing grows by while lines would be too close.</summary>
    public long Multiplier { get; set; } = 2;

    public void Attach() { }
    public void Detach() { }
    public void UpdateScene(MapScene scene) { }

    public void Render(DrawingContext context, MapScene scene)
    {
        if (!IsVisible || scene.Viewport.IsEmpty)
            return;

        var viewport = scene.Viewport;
        var spacing = GetSpacing(viewport.BlocksPerPixel);
        var halfPixel = 0.5 / scene.RenderScaling;

        for (var x = FirstLine(viewport.Left, spacing); x <= viewport.Right; x += spacing)
        {
            var screenX = scene.Snap(viewport.ToScreen(x, 0).X) + halfPixel;
            context.DrawLine(x == 0 ? _axisPen : _linePen, new Point(screenX, 0), new Point(screenX, viewport.Height));
            DrawLabel(context, x, new Point(screenX + LabelMargin, LabelMargin));
        }

        for (var z = FirstLine(viewport.Top, spacing); z <= viewport.Bottom; z += spacing)
        {
            var screenY = scene.Snap(viewport.ToScreen(0, z).Y) + halfPixel;
            context.DrawLine(z == 0 ? _axisPen : _linePen, new Point(0, screenY), new Point(viewport.Width, screenY));
            DrawLabel(context, z, new Point(LabelMargin, screenY + LabelMargin));
        }
    }

    private long GetSpacing(double blocksPerPixel)
    {
        var spacing = Math.Max(BaseSpacing, 1);
        var multiplier = Math.Max(Multiplier, 2);
        while (spacing / blocksPerPixel < MinSpacingPixels)
            spacing *= multiplier;
        return spacing;
    }

    private static long FirstLine(double edge, long spacing) => (long)Math.Ceiling(edge / spacing) * spacing;

    private static void DrawLabel(DrawingContext context, long block, Point position)
    {
        var text = block.ToString(CultureInfo.InvariantCulture);
        context.DrawText(CreateText(text, _labelShadowBrush), position + _shadowOffset);
        context.DrawText(CreateText(text, _labelBrush), position);
    }

    private static FormattedText CreateText(string text, IBrush brush)
        => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, Typeface.Default, LabelFontSize, brush);
}

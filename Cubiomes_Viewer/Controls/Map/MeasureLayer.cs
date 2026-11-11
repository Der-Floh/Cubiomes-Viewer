using System.Globalization;

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>Draws the line of a distance measurement between two blocks together with its length.</summary>
public sealed class MeasureLayer : IMapLayer
{
    private const double BlockCenter = 0.5;
    private const double LineThickness = 2;
    private const double OutlineThickness = 4;
    private const double EndpointRadius = 4;
    private const double LabelFontSize = 13;
    private const double LabelGap = 8;
    private const double LabelCornerRadius = 4;
    private static readonly Vector _labelPadding = new(6, 2);
    private static readonly RenderOptions _renderOptions = new() { EdgeMode = EdgeMode.Antialias };

    public event EventHandler? Invalidated;

    public bool IsVisible { get; set; } = true;

    /// <summary>The block the distance is measured from, or <see langword="null"/> when nothing is measured.</summary>
    public BlockPos? Start
    {
        get;
        set
        {
            field = value;
            Invalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>The block the distance is measured to, usually the one under the pointer.</summary>
    public BlockPos? End
    {
        get;
        set
        {
            field = value;
            if (Start is not null)
                Invalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    private readonly IMapStyleService _styleService;

    private ImmutableSolidColorBrush LineBrush => field ??= new ImmutableSolidColorBrush(_styleService.MeasureColor);
    private IPen LinePen => field ??= new ImmutablePen(LineBrush, LineThickness);
    private ImmutableSolidColorBrush OutlineBrush => field ??= new ImmutableSolidColorBrush(_styleService.OutlineColor);
    private IPen OutlinePen => field ??= new ImmutablePen(OutlineBrush, OutlineThickness);
    private IPen EndpointPen => field ??= new ImmutablePen(OutlineBrush);

    public MeasureLayer(IMapStyleService styleService)
    {
        _styleService = styleService;
    }

    public void Attach() { }
    public void Detach() { }
    public void UpdateScene(MapScene scene) { }

    public void Render(DrawingContext context, MapScene scene)
    {
        if (!IsVisible || Start is not { } start)
            return;

        using (context.PushRenderOptions(_renderOptions))
        {
            var from = ToScreen(scene, start);
            if (End is { } end && end != start)
            {
                var to = ToScreen(scene, end);
                context.DrawLine(OutlinePen, from, to);
                context.DrawLine(LinePen, from, to);
                DrawEndpoint(context, to);
                DrawLabel(context, scene, start.DistanceTo(end).ToString("0.#", CultureInfo.InvariantCulture), to);
            }

            DrawEndpoint(context, from);
        }
    }

    private static Point ToScreen(MapScene scene, BlockPos block)
    {
        var (x, y) = scene.Viewport.ToScreen(block.X + BlockCenter, block.Z + BlockCenter);
        return new Point(x, y);
    }

    private void DrawEndpoint(DrawingContext context, Point center)
        => context.DrawEllipse(LineBrush, EndpointPen, center, EndpointRadius, EndpointRadius);

    private void DrawLabel(DrawingContext context, MapScene scene, string text, Point endpoint)
    {
        var label = new FormattedText(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(Typeface.Default.FontFamily, weight: FontWeight.SemiBold), LabelFontSize, LineBrush);
        var backing = GetLabelBacking(scene, endpoint, new Size(label.Width + 2 * _labelPadding.X, label.Height + 2 * _labelPadding.Y));

        context.DrawRectangle(OutlineBrush, null, backing, LabelCornerRadius, LabelCornerRadius);
        context.DrawText(label, backing.TopLeft + _labelPadding);
    }

    /// <summary>Places the label above the end of the line, moved back inside the map where it would stick out.</summary>
    private static Rect GetLabelBacking(MapScene scene, Point endpoint, Size size)
    {
        var x = Math.Clamp(endpoint.X - size.Width / 2, 0, Math.Max(0, scene.Viewport.Width - size.Width));
        var y = Math.Clamp(endpoint.Y - EndpointRadius - LabelGap - size.Height, 0, Math.Max(0, scene.Viewport.Height - size.Height));
        return new Rect(new Point(x, y), size);
    }
}

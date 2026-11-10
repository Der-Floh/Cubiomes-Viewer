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
    private static readonly Vector _labelOffset = new(10, -22);
    private static readonly Vector _shadowOffset = new(1, 1);
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
                DrawLabel(context, start.DistanceTo(end).ToString("0.#", CultureInfo.InvariantCulture), to + _labelOffset);
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

    private void DrawLabel(DrawingContext context, string text, Point position)
    {
        context.DrawText(CreateText(text, OutlineBrush), position + _shadowOffset);
        context.DrawText(CreateText(text, LineBrush), position);
    }

    private static FormattedText CreateText(string text, IBrush brush)
        => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, new Typeface(Typeface.Default.FontFamily, weight: FontWeight.SemiBold), LabelFontSize, brush);
}

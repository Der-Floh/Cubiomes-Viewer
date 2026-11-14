using System.Globalization;

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>
/// Draws measured lines alike wherever they appear: an outlined line between the middles of two blocks, a dot at each end and a label with
/// the length, in the measuring colour or, for the picked one, in the selection colour.
/// </summary>
public sealed class MeasurePainter
{
    private const double BlockMiddle = 0.5;
    private const double LineThickness = 2;
    private const double OutlineThickness = 4;
    private const double EndpointRadius = 4;
    private const double LabelFontSize = 13;
    private const double LabelGap = 8;
    private const double LabelCornerRadius = 4;
    private const double HitTolerance = 6;
    private const string LengthFormat = "0.#";
    private const string NameSeparator = "  ·  ";
    private static readonly Vector _labelPadding = new(6, 2);
    private static readonly Typeface _labelTypeface = new(Typeface.Default.FontFamily, weight: FontWeight.SemiBold);

    private readonly IMapStyleService _styleService;

    private Look Measuring => field ??= new Look(_styleService.MeasureColor, _styleService.OutlineColor);
    private Look Picked => field ??= new Look(_styleService.SelectionColor, _styleService.OutlineColor);

    public MeasurePainter(IMapStyleService styleService)
    {
        _styleService = styleService;
    }

    /// <summary>The label of a length, with the name in front where there is one.</summary>
    public static string ToLabel(double length, string? name)
    {
        var text = length.ToString(LengthFormat, CultureInfo.InvariantCulture);
        return name is null ? text : $"{name}{NameSeparator}{text}";
    }

    /// <summary>Where the middle of a block lies on a view of the map.</summary>
    public static Point ToScreen(MapViewport viewport, BlockPos block)
    {
        var (x, y) = viewport.ToScreen(block.X + BlockMiddle, block.Z + BlockMiddle);
        return new Point(x, y);
    }

    /// <summary>Draws the line of a measurement in progress, with its label above the end, kept inside the view.</summary>
    public void DrawOpen(DrawingContext context, MapViewport viewport, BlockPos start, BlockPos? end)
    {
        ArgumentNullException.ThrowIfNull(context);

        var from = ToScreen(viewport, start);
        if (end is not { } last || last == start)
        {
            DrawEndpoint(context, Measuring, from);
            return;
        }

        var to = ToScreen(viewport, last);
        DrawLine(context, Measuring, from, to);
        var label = CreateLabel(ToLabel(start.DistanceTo(last), null), Measuring);
        DrawLabel(context, Measuring, label, GetLabelAbove(viewport, to, label));
    }

    /// <summary>Draws a kept measurement with its label on the middle of the line.</summary>
    public void DrawKept(DrawingContext context, MapViewport viewport, MapMeasurement measurement, bool isPicked)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(measurement);

        var look = isPicked ? Picked : Measuring;
        var (from, to) = (ToScreen(viewport, measurement.Start), ToScreen(viewport, measurement.End));
        DrawLine(context, look, from, to);
        var label = CreateLabel(ToLabel(measurement.Length, measurement.Name), look);
        DrawLabel(context, look, label, GetLabelOn(from, to, label));
    }

    /// <summary>Where the label of a kept measurement is drawn.</summary>
    public Rect GetLabelBounds(MapViewport viewport, MapMeasurement measurement)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        var label = CreateLabel(ToLabel(measurement.Length, measurement.Name), Measuring);
        return GetLabelOn(ToScreen(viewport, measurement.Start), ToScreen(viewport, measurement.End), label);
    }

    /// <summary>Whether a point of the view lies on a kept measurement: within a few pixels of its line, or on its label.</summary>
    public bool HitTest(MapViewport viewport, MapMeasurement measurement, Point point)
    {
        ArgumentNullException.ThrowIfNull(measurement);

        var (from, to) = (ToScreen(viewport, measurement.Start), ToScreen(viewport, measurement.End));
        return GetDistanceToSegment(point, from, to) <= HitTolerance || GetLabelBounds(viewport, measurement).Contains(point);
    }

    private static void DrawLine(DrawingContext context, Look look, Point from, Point to)
    {
        context.DrawLine(look.OutlinePen, from, to);
        context.DrawLine(look.LinePen, from, to);
        DrawEndpoint(context, look, from);
        DrawEndpoint(context, look, to);
    }

    private static void DrawEndpoint(DrawingContext context, Look look, Point center)
        => context.DrawEllipse(look.LineBrush, look.EndpointPen, center, EndpointRadius, EndpointRadius);

    private static void DrawLabel(DrawingContext context, Look look, FormattedText label, Rect backing)
    {
        context.DrawRectangle(look.OutlineBrush, null, backing, LabelCornerRadius, LabelCornerRadius);
        context.DrawText(label, backing.TopLeft + _labelPadding);
    }

    private static FormattedText CreateLabel(string text, Look look)
        => new(text, CultureInfo.InvariantCulture, FlowDirection.LeftToRight, _labelTypeface, LabelFontSize, look.LineBrush);

    private static Size GetBackingSize(FormattedText label) => new(label.Width + 2 * _labelPadding.X, label.Height + 2 * _labelPadding.Y);

    /// <summary>Places a label above the end of a line, moved back inside the view where it would stick out.</summary>
    private static Rect GetLabelAbove(MapViewport viewport, Point endpoint, FormattedText label)
    {
        var size = GetBackingSize(label);
        var x = Math.Clamp(endpoint.X - size.Width / 2, 0, Math.Max(0, viewport.Width - size.Width));
        var y = Math.Clamp(endpoint.Y - EndpointRadius - LabelGap - size.Height, 0, Math.Max(0, viewport.Height - size.Height));
        return new Rect(new Point(x, y), size);
    }

    /// <summary>Places a label on the middle of a line, where it moves along with the map.</summary>
    private static Rect GetLabelOn(Point from, Point to, FormattedText label)
    {
        var size = GetBackingSize(label);
        var (middleX, middleY) = ((from.X + to.X) / 2, (from.Y + to.Y) / 2);
        return new Rect(new Point(middleX - size.Width / 2, middleY - size.Height / 2), size);
    }

    private static double GetDistanceToSegment(Point point, Point from, Point to)
    {
        var (segmentX, segmentY) = (to.X - from.X, to.Y - from.Y);
        var lengthSquared = segmentX * segmentX + segmentY * segmentY;
        var share = lengthSquared == 0 ? 0 : Math.Clamp(((point.X - from.X) * segmentX + (point.Y - from.Y) * segmentY) / lengthSquared, 0, 1);
        return double.Hypot(point.X - (from.X + segmentX * share), point.Y - (from.Y + segmentY * share));
    }

    /// <summary>The pens and brushes of one colour of measurement.</summary>
    private sealed class Look(Color line, Color outline)
    {
        public ImmutableSolidColorBrush LineBrush { get; } = new(line);
        public ImmutableSolidColorBrush OutlineBrush { get; } = new(outline);
        public IPen LinePen => field ??= new ImmutablePen(LineBrush, LineThickness);
        public IPen OutlinePen => field ??= new ImmutablePen(OutlineBrush, OutlineThickness);
        public IPen EndpointPen => field ??= new ImmutablePen(OutlineBrush);
    }
}

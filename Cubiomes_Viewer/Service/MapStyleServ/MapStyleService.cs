using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;

using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Service.MapStyleServ;

public sealed class MapStyleService : IMapStyleService
{
    private const string IconKeySuffix = "Icon";
    private const string MarkerColorKeyPrefix = "Marker.";
    private const string SpawnKey = "Spawn";
    private const string StrongholdKey = "Stronghold";
    private const string GlyphColorKey = "Marker.Glyph";
    private const string OutlineColorKey = "Marker.Outline";
    private const string SlimeChunkColorKey = "SlimeChunkColor";
    private const string MeasureColorKey = "MeasureColor";
    private const double MarkerSideLength = 22;
    private const double GlyphDesignSize = 24;
    private const double GlyphSize = 14;
    private const double CornerRadius = 5;

    public double MarkerSize => MarkerSideLength;

    public MapMarker Spawn => field ??= CreateMarker(SpawnKey);
    public MapMarker Stronghold => field ??= CreateMarker(StrongholdKey);
    public Color SlimeChunkColor => _slimeChunkColor ??= GetResource<Color>(SlimeChunkColorKey);
    public Color MeasureColor => _measureColor ??= GetResource<Color>(MeasureColorKey);
    public Color OutlineColor => _outlineColor ??= GetResource<Color>(OutlineColorKey);

    private readonly Dictionary<StructureType, MapMarker> _structureMarkers = [];
    private Color? _slimeChunkColor;
    private Color? _measureColor;
    private Color? _outlineColor;

    private IBrush GlyphBrush => field ??= new ImmutableSolidColorBrush(GetResource<Color>(GlyphColorKey));
    private IPen OutlinePen => field ??= new ImmutablePen(new ImmutableSolidColorBrush(OutlineColor));

    public MapMarker GetStructureMarker(StructureType type)
    {
        if (!_structureMarkers.TryGetValue(type, out var marker))
        {
            marker = CreateMarker(type.ToString());
            _structureMarkers.Add(type, marker);
        }

        return marker;
    }

    public void DrawMarker(DrawingContext context, MapMarker marker, Point center)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(marker);

        var badge = new Rect(center.X - MarkerSize / 2, center.Y - MarkerSize / 2, MarkerSize, MarkerSize);
        context.DrawRectangle(marker.Fill, OutlinePen, badge, CornerRadius, CornerRadius);

        var scale = GlyphSize / GlyphDesignSize;
        var transform = Matrix.CreateScale(scale, scale) * Matrix.CreateTranslation(center.X - GlyphSize / 2, center.Y - GlyphSize / 2);
        using (context.PushTransform(transform))
            context.DrawGeometry(GlyphBrush, null, marker.Glyph);
    }

    private static MapMarker CreateMarker(string name)
        => new(GetResource<Geometry>(name + IconKeySuffix), new ImmutableSolidColorBrush(GetResource<Color>(MarkerColorKeyPrefix + name)));

    private static T GetResource<T>(string key)
    {
        var application = Application.Current ?? throw new InvalidOperationException("Map styles need a running application.");
        return application.TryGetResource(key, application.ActualThemeVariant, out var resource) && resource is T typed
            ? typed
            : throw new InvalidOperationException($"The application resource '{key}' of type {typeof(T).Name} is missing.");
    }
}

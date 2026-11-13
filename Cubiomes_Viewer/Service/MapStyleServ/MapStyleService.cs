using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Svg.Skia;

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
    private const string SelectionColorKey = "SelectionColor";
    private const double MarkerSideLength = 22;
    private const double GlyphSize = 14;
    private const double CornerRadius = 5;

    private static readonly Uri _assetBaseUri = new($"avares://{typeof(MapStyleService).Assembly.GetName().Name}/");

    public double MarkerSize => MarkerSideLength * MarkerScale;

    public double MarkerScale { get; set; } = 1;

    public MapMarker Spawn => field ??= CreateMarker(SpawnKey);
    public MapMarker Stronghold => field ??= CreateMarker(StrongholdKey);
    public Color SlimeChunkColor => _slimeChunkColor ??= GetResource<Color>(SlimeChunkColorKey);
    public Color MeasureColor => _measureColor ??= GetResource<Color>(MeasureColorKey);
    public Color OutlineColor => _outlineColor ??= GetResource<Color>(OutlineColorKey);
    public Color SelectionColor => _selectionColor ??= GetResource<Color>(SelectionColorKey);

    private readonly Dictionary<StructureType, MapMarker> _structureMarkers = [];
    private readonly Dictionary<Dimension, MapMarker> _dimensionMarkers = [];
    private Color? _slimeChunkColor;
    private Color? _measureColor;
    private Color? _outlineColor;
    private Color? _selectionColor;

    private Color GlyphColor => _glyphColor ??= GetResource<Color>(GlyphColorKey);
    private IPen OutlinePen => field ??= new ImmutablePen(new ImmutableSolidColorBrush(OutlineColor));
    private Color? _glyphColor;

    public MapMarker GetStructureMarker(StructureType type)
    {
        if (!_structureMarkers.TryGetValue(type, out var marker))
        {
            marker = CreateMarker(type.ToString());
            _structureMarkers.Add(type, marker);
        }

        return marker;
    }

    public MapMarker GetDimensionMarker(Dimension dimension)
    {
        if (!_dimensionMarkers.TryGetValue(dimension, out var marker))
        {
            marker = CreateMarker(dimension.ToString());
            _dimensionMarkers.Add(dimension, marker);
        }

        return marker;
    }

    public void DrawMarker(DrawingContext context, MapMarker marker, Point center)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(marker);

        var badge = new Rect(center.X - MarkerSize / 2, center.Y - MarkerSize / 2, MarkerSize, MarkerSize);
        context.DrawRectangle(marker.Fill, OutlinePen, badge, CornerRadius * MarkerScale, CornerRadius * MarkerScale);

        var glyphSize = GlyphSize * MarkerScale;
        context.DrawImage(marker.Glyph, new Rect(center.X - glyphSize / 2, center.Y - glyphSize / 2, glyphSize, glyphSize));
    }

    private MapMarker CreateMarker(string name)
        => new(CreateGlyph(GetResource<string>(name + IconKeySuffix)), new ImmutableSolidColorBrush(GetResource<Color>(MarkerColorKeyPrefix + name)));

    private SvgImage CreateGlyph(string path) => new() { Source = SvgSource.Load(path, _assetBaseUri), CurrentColor = GlyphColor };

    private static T GetResource<T>(string key)
    {
        var application = Application.Current ?? throw new InvalidOperationException("Map styles need a running application.");
        return application.TryGetResource(key, application.ActualThemeVariant, out var resource) && resource is T typed
            ? typed
            : throw new InvalidOperationException($"The application resource '{key}' of type {typeof(T).Name} is missing.");
    }
}

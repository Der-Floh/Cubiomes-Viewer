using System.Globalization;

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Svg.Skia;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Service.MapStyleServ;

public sealed class MapStyleService : IMapStyleService
{
    private const string IconKeySuffix = "Icon";
    private const string MarkerColorKeyPrefix = "Marker.";
    private const string BiomeIconPathFormat = "/Assets/Icons/MapIcons/Biomes/{0}.svg";
    private const string PinColorKey = "Marker.Pin";
    private const string GlyphColorKey = "Marker.Glyph";
    private const string DarkGlyphColorKey = "Marker.GlyphDark";
    private const string OutlineColorKey = "Marker.Outline";
    private const string SlimeChunkColorKey = "SlimeChunkColor";
    private const string MeasureColorKey = "MeasureColor";
    private const string SelectionColorKey = "SelectionColor";
    private const double MarkerSideLength = 22;
    private const double PinSideLength = 52;
    private const double RedWeight = 0.2126;
    private const double GreenWeight = 0.7152;
    private const double BlueWeight = 0.0722;
    private const double LinearThreshold = 0.04045;
    private const double LinearSlope = 12.92;
    private const double GammaOffset = 0.055;
    private const double Gamma = 2.4;
    private const double ContrastOffset = 0.05;

    private static readonly Uri _assetBaseUri = new($"avares://{typeof(MapStyleService).Assembly.GetName().Name}/");

    public double PinSize => Math.Max(PinSideLength * MarkerScale, PinPainter.SmallestSizeWithRoom);

    public double MarkerScale { get; set; } = 1;

    public MapMarker Spawn => GetMarker(MarkerNames.Spawn);
    public MapMarker Stronghold => GetMarker(MarkerNames.Stronghold);
    public PinLook PlainPin => field ??= new PinLook(null, new ImmutableSolidColorBrush(GetResource<Color>(PinColorKey)), null);
    public Color SlimeChunkColor => _slimeChunkColor ??= GetResource<Color>(SlimeChunkColorKey);
    public Color MeasureColor => _measureColor ??= GetResource<Color>(MeasureColorKey);
    public Color OutlineColor => _outlineColor ??= GetResource<Color>(OutlineColorKey);
    public Color SelectionColor => _selectionColor ??= GetResource<Color>(SelectionColorKey);

    private readonly IBiomeCatalog _biomeCatalog;
    private readonly IVersionCatalog _versionCatalog;
    private readonly Dictionary<string, MapMarker> _markers = new(StringComparer.Ordinal);
    private readonly Dictionary<string, PinLook> _pinLooks = new(StringComparer.Ordinal);
    private readonly Dictionary<int, MapMarker> _biomeMarkers = [];
    private Color? _slimeChunkColor;
    private Color? _measureColor;
    private Color? _outlineColor;
    private Color? _selectionColor;

    private Color GlyphColor => _glyphColor ??= GetResource<Color>(GlyphColorKey);
    private IBrush OutlineBrush => field ??= new ImmutableSolidColorBrush(OutlineColor);
    private IBrush PinFaceBrush => field ??= new ImmutableSolidColorBrush(GlyphColor);
    private Color? _glyphColor;

    public MapStyleService(IBiomeCatalog biomeCatalog, IVersionCatalog versionCatalog)
    {
        _biomeCatalog = biomeCatalog;
        _versionCatalog = versionCatalog;
    }

    public MapMarker GetStructureMarker(StructureType type) => GetMarker(MarkerNames.Of(type));

    public MapMarker GetBiomeMarker(int biome)
    {
        if (!_biomeMarkers.TryGetValue(biome, out var marker))
        {
            var icon = _biomeCatalog.GetName(_versionCatalog.Newest, biome)
                ?? throw new ArgumentOutOfRangeException(nameof(biome), biome, "The generator knows no biome with this id.");
            var color = _biomeCatalog.DefaultPalette[biome];
            var fill = Color.FromRgb(color.Red, color.Green, color.Blue);
            marker = new MapMarker(CreateBiomeGlyph(icon, GetContrastingGlyphColor(color)), new ImmutableSolidColorBrush(fill));
            _biomeMarkers.Add(biome, marker);
        }

        return marker;
    }

    public MapMarker GetDimensionMarker(Dimension dimension) => GetMarker(MarkerNames.Of(dimension));

    public PinLook GetPinLook(string? icon)
    {
        if (icon is null || !IsMarkerLook(icon))
            return PlainPin;

        if (!_pinLooks.TryGetValue(icon, out var look))
        {
            var color = GetResource<Color>(MarkerColorKeyPrefix + icon);
            look = new PinLook(icon, new ImmutableSolidColorBrush(color), CreateGlyph(icon, color));
            _pinLooks.Add(icon, look);
        }

        return look;
    }

    public double GetMarkerSize(PixelGrid pixels, double magnification = 1) => MarkerBadgePainter.GetSide(GetWantedMarkerSize(magnification), pixels);

    public void DrawMarker(DrawingContext context, MapMarker marker, Point center, PixelGrid pixels, double magnification = 1)
    {
        ArgumentNullException.ThrowIfNull(marker);

        MarkerBadgePainter.Draw(context, marker.Fill, marker.Glyph, center, GetWantedMarkerSize(magnification), pixels, OutlineBrush);
    }

    public void DrawPin(DrawingContext context, PinLook look, Point tip, PixelGrid pixels, double magnification = 1)
    {
        ArgumentNullException.ThrowIfNull(look);

        PinPainter.Draw(context, look.Fill, look.Glyph, tip, PinSize * magnification, pixels, PinFaceBrush, OutlineBrush);
    }

    public Rect GetPinBounds(Point tip, double magnification = 1) => PinPainter.GetBounds(tip, PinSize * magnification);

    private double GetWantedMarkerSize(double magnification) => MarkerSideLength * MarkerScale * magnification;

    /// <summary>Returns the marker of a structure type, landmark or dimension by the name its resources are keyed with.</summary>
    private MapMarker GetMarker(string name)
    {
        if (!_markers.TryGetValue(name, out var marker))
        {
            marker = CreateMarker(name);
            _markers.Add(name, marker);
        }

        return marker;
    }

    private MapMarker CreateMarker(string name)
        => new(CreateGlyph(name, GlyphColor), new ImmutableSolidColorBrush(GetResource<Color>(MarkerColorKeyPrefix + name)));

    /// <summary>Loads the glyph of a marker by the name its resources are keyed with, drawn in one colour.</summary>
    private static SvgImage CreateGlyph(string name, Color color) => LoadGlyph(GetResource<string>(name + IconKeySuffix), color);

    /// <summary>Loads the glyph of a biome, named like the biome.</summary>
    private static SvgImage CreateBiomeGlyph(string icon, Color color)
        => LoadGlyph(string.Format(CultureInfo.InvariantCulture, BiomeIconPathFormat, icon), color);

    private static SvgImage LoadGlyph(string path, Color color) => new() { Source = SvgSource.Load(path, _assetBaseUri), CurrentColor = color };

    /// <summary>The light or the dark glyph colour, whichever stands out more against a badge, judged by relative luminance as in WCAG.</summary>
    private static Color GetContrastingGlyphColor(RgbColor background)
    {
        var (light, dark) = (GetResource<Color>(GlyphColorKey), GetResource<Color>(DarkGlyphColorKey));
        var luminance = GetLuminance(background.Red, background.Green, background.Blue);
        var againstLight = (GetLuminance(light.R, light.G, light.B) + ContrastOffset) / (luminance + ContrastOffset);
        var againstDark = (luminance + ContrastOffset) / (GetLuminance(dark.R, dark.G, dark.B) + ContrastOffset);
        return againstLight >= againstDark ? light : dark;
    }

    private static double GetLuminance(byte red, byte green, byte blue)
        => RedWeight * ToLinear(red) + GreenWeight * ToLinear(green) + BlueWeight * ToLinear(blue);

    private static double ToLinear(byte channel)
    {
        var value = channel / (double)byte.MaxValue;
        return value <= LinearThreshold ? value / LinearSlope : Math.Pow((value + GammaOffset) / (1 + GammaOffset), Gamma);
    }

    /// <summary>Whether a name has the glyph and the colour of a marker; interface icons share the glyph key pattern but have no colour.</summary>
    private static bool IsMarkerLook(string name) => HasResource<string>(name + IconKeySuffix) && HasResource<Color>(MarkerColorKeyPrefix + name);

    private static bool HasResource<T>(string key)
        => Application.Current is { } application && application.TryGetResource(key, application.ActualThemeVariant, out var resource) && resource is T;

    private static T GetResource<T>(string key)
    {
        var application = Application.Current ?? throw new InvalidOperationException("Map styles need a running application.");
        return application.TryGetResource(key, application.ActualThemeVariant, out var resource) && resource is T typed
            ? typed
            : throw new InvalidOperationException($"The application resource '{key}' of type {typeof(T).Name} is missing.");
    }
}

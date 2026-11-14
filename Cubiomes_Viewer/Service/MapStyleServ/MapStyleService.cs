using System.Globalization;

using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using Avalonia.Svg.Skia;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Extensions;

namespace Cubiomes_Viewer.Service.MapStyleServ;

public sealed class MapStyleService : IMapStyleService
{
    private const string IconKeySuffix = "Icon";
    private const string MarkerColorKeyPrefix = "Marker.";
    private const string MarkerStyleKey = "MarkerStyle";
    private const string BiomeIconPathFormat = "/Assets/Icons/Biomes/{0}/{1}.svg";
    private const string PinColorKey = "Marker.Pin";
    private const string GlyphColorKey = "Marker.Glyph";
    private const string DarkGlyphColorKey = "Marker.GlyphDark";
    private const string OutlineColorKey = "Marker.Outline";
    private const string SlimeChunkColorKey = "SlimeChunkColor";
    private const string MeasureColorKey = "MeasureColor";
    private const string SelectionColorKey = "SelectionColor";
    private const double MarkerSideLength = 22;
    private const double PinSideLength = 32;
    private const double RedWeight = 0.2126;
    private const double GreenWeight = 0.7152;
    private const double BlueWeight = 0.0722;
    private const double LinearThreshold = 0.04045;
    private const double LinearSlope = 12.92;
    private const double GammaOffset = 0.055;
    private const double Gamma = 2.4;
    private const double ContrastOffset = 0.05;

    private static readonly Uri _assetBaseUri = new($"avares://{typeof(MapStyleService).Assembly.GetName().Name}/");

    public double MarkerSize => MarkerSideLength * MarkerScale;

    public double PinSize => PinSideLength * MarkerScale;

    public double MarkerScale { get; set; } = 1;

    public MarkerStyle MarkerStyle
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            Application.Current?.UseStyleDictionary(MarkerStyleKey, value.ToString(), GetSource(value));
            foreach (var (name, marker) in _markers)
                marker.Glyph = CreateGlyph(name, GlyphColor);
            foreach (var (name, look) in _pinLooks)
                look.Glyph = CreateGlyph(name, GetResource<Color>(MarkerColorKeyPrefix + name));
            foreach (var biome in _biomeMarkers.Values)
                biome.Marker.Glyph = CreateBiomeGlyph(biome.Icon, biome.GlyphColor);
        }
    }

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
    private readonly Dictionary<int, BiomeMarker> _biomeMarkers = [];
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
        if (!_biomeMarkers.TryGetValue(biome, out var entry))
        {
            var icon = _biomeCatalog.GetName(_versionCatalog.Newest, biome)
                ?? throw new ArgumentOutOfRangeException(nameof(biome), biome, "The generator knows no biome with this id.");
            var color = _biomeCatalog.DefaultPalette[biome];
            var fill = Color.FromRgb(color.Red, color.Green, color.Blue);
            var glyphColor = GetContrastingGlyphColor(color);
            entry = new BiomeMarker(new MapMarker(CreateBiomeGlyph(icon, glyphColor), new ImmutableSolidColorBrush(fill)), icon, glyphColor);
            _biomeMarkers.Add(biome, entry);
        }

        return entry.Marker;
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

    public void DrawMarker(DrawingContext context, MapMarker marker, Point center, PixelGrid pixels, double magnification = 1)
    {
        ArgumentNullException.ThrowIfNull(marker);

        MarkerBadgePainter.Draw(context, marker.Fill, marker.Glyph, center, MarkerSize * magnification, pixels, OutlineBrush);
    }

    public void DrawPin(DrawingContext context, PinLook look, Point tip, PixelGrid pixels, double magnification = 1)
    {
        ArgumentNullException.ThrowIfNull(look);

        PinPainter.Draw(context, look.Fill, look.Glyph, tip, PinSize * magnification, pixels, PinFaceBrush, OutlineBrush);
    }

    public Rect GetPinBounds(Point tip, double magnification = 1) => PinPainter.GetBounds(tip, PinSize * magnification);

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

    /// <summary>Loads the glyph of a marker in the current marker style, drawn in one colour.</summary>
    private static SvgImage CreateGlyph(string name, Color color)
        => new() { Source = SvgSource.Load(GetResource<string>(name + IconKeySuffix), _assetBaseUri), CurrentColor = color };

    /// <summary>Loads the glyph of a biome, named like the biome, from the folder of the current marker style.</summary>
    private static SvgImage CreateBiomeGlyph(string icon, Color color)
    {
        var path = string.Format(CultureInfo.InvariantCulture, BiomeIconPathFormat, GetResource<string>(MarkerStyleKey), icon);
        return new SvgImage { Source = SvgSource.Load(path, _assetBaseUri), CurrentColor = color };
    }

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

    /// <summary>A biome's badge with what its glyph is loaded from, so that a change of the marker style can load it anew.</summary>
    private sealed record BiomeMarker(MapMarker Marker, string Icon, Color GlyphColor);

    private static string GetSource(MarkerStyle style) => style switch
    {
        MarkerStyle.Blocky => "Accents/MarkersBlocky.axaml",
        MarkerStyle.Clean => "Accents/MarkersClean.axaml",
        _ => throw new NotSupportedException($"Unsupported marker style: {style}"),
    };

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

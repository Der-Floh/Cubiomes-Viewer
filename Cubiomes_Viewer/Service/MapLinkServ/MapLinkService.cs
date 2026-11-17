using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Service.MapLinkServ;

public sealed class MapLinkService : IMapLinkService
{
    /// <summary>The most pins a link carries, so that its address stays short enough for web servers.</summary>
    public const int MaxPins = 100;

    /// <summary>The longest name or look of a pin a link carries, as long as a name typed in the callout can be.</summary>
    public const int MaxPinTextLength = 64;

    private const string SeedKey = "seed";
    private const string VersionKey = "version";
    private const string DimensionKey = "dimension";
    private const string XKey = "x";
    private const string ZKey = "z";
    private const string ScaleKey = "scale";
    private const string LargeBiomesKey = "largebiomes";
    private const string LayerKey = "layer";
    private const string SampleHeightKey = "y";
    private const string OctavesKey = "octaves";
    private const string PinKey = "pin";
    private const string ScaleFormat = "0.#####";
    private const string TrueFlag = "1";
    private const string FalseFlag = "0";
    private const char QueryStart = '?';
    private const char FragmentStart = '#';
    private const char ParameterSeparator = '&';
    private const char ValueSeparator = '=';
    private const char PinPartSeparator = ',';
    private const char EscapedSpace = '+';
    private const int PinPositionParts = 2;
    private const int PinNamePart = 2;
    private const int PinIconPart = 3;
    private const int PinParts = 4;
    private const string NoSeedProblem = "The link has no seed.";
    private static readonly string[] _addressSchemes = ["http://", "https://"];
    private static readonly FrozenDictionary<string, bool> _flags = new Dictionary<string, bool>
    {
        [TrueFlag] = true,
        [bool.TrueString] = true,
        [FalseFlag] = false,
        [bool.FalseString] = false,
    }.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    private static readonly FrozenDictionary<string, Dimension> _dimensions = CreateNames<Dimension>();
    private static readonly FrozenDictionary<string, LayerMode> _layers = CreateNames<LayerMode>();

    private readonly IVersionCatalog _versionCatalog;
    private readonly ISeedParser _seedParser;

    public MapLinkService(IVersionCatalog versionCatalog, ISeedParser seedParser)
    {
        _versionCatalog = versionCatalog;
        _seedParser = seedParser;
    }

    public string Format(MapLink link, string baseAddress)
    {
        ArgumentNullException.ThrowIfNull(link);
        ArgumentNullException.ThrowIfNull(baseAddress);

        List<string> parameters =
        [
            ToParameter(SeedKey, link.Seed.ToString(CultureInfo.InvariantCulture)),
            ToParameter(VersionKey, _versionCatalog.GetName(link.Version)),
            ToParameter(DimensionKey, ToName(link.Dimension)),
            ToParameter(XKey, link.Center.X.ToString(CultureInfo.InvariantCulture)),
            ToParameter(ZKey, link.Center.Z.ToString(CultureInfo.InvariantCulture)),
            ToParameter(ScaleKey, link.BlocksPerPixel.ToString(ScaleFormat, CultureInfo.InvariantCulture)),
        ];
        if (link.LargeBiomes)
            parameters.Add(ToParameter(LargeBiomesKey, TrueFlag));
        if (link.Layer != LayerMode.Biomes)
            parameters.Add(ToParameter(LayerKey, ToName(link.Layer)));
        if (link.SampleHeight != ViewSettings.DefaultSampleHeight)
            parameters.Add(ToParameter(SampleHeightKey, link.SampleHeight.ToString(CultureInfo.InvariantCulture)));
        if (link.Octaves != 0)
            parameters.Add(ToParameter(OctavesKey, link.Octaves.ToString(CultureInfo.InvariantCulture)));
        parameters.AddRange(link.Pins.Take(MaxPins).Select(ToPinParameter));

        return $"{baseAddress}{QueryStart}{string.Join(ParameterSeparator, parameters)}";
    }

    public bool TryParse(string? text, MapLink current, [NotNullWhen(true)] out MapLink? link, out string? problem)
    {
        ArgumentNullException.ThrowIfNull(current);

        link = null;
        problem = null;
        if (string.IsNullOrWhiteSpace(text))
            return false;

        var (query, isAddress) = GetQuery(text.Trim());
        var reader = new ParameterReader(ReadParameters(query));
        if (!reader.TryGetText(SeedKey, out var seed) || string.IsNullOrWhiteSpace(seed))
        {
            problem = isAddress ? NoSeedProblem : null;
            return false;
        }

        var center = new BlockPos(
            reader.Read(XKey, TryReadCoordinate, current.Center.X, current.Center.X),
            reader.Read(ZKey, TryReadCoordinate, current.Center.Z, current.Center.Z));
        link = new MapLink(
            _seedParser.Parse(seed).Seed,
            reader.Read<McVersion>(VersionKey, _versionCatalog.TryGetVersion, current.Version, current.Version),
            reader.Read<Dimension>(DimensionKey, _dimensions.TryGetValue, current.Dimension, current.Dimension),
            reader.Read<bool>(LargeBiomesKey, _flags.TryGetValue, false, current.LargeBiomes),
            center,
            reader.Read(ScaleKey, TryReadScale, current.BlocksPerPixel, current.BlocksPerPixel),
            reader.Read<LayerMode>(LayerKey, _layers.TryGetValue, LayerMode.Biomes, current.Layer),
            reader.Read(SampleHeightKey, TryReadSampleHeight, ViewSettings.DefaultSampleHeight, current.SampleHeight),
            reader.Read(OctavesKey, TryReadOctaves, 0, current.Octaves))
        {
            Pins = reader.ReadPins(),
        };
        problem = reader.Problem;
        return true;
    }

    /// <summary>The query of an address or of text that starts with one; other text counts as bare parameters.</summary>
    private static (string Query, bool IsAddress) GetQuery(string text)
    {
        var withoutFragment = text.Split(FragmentStart, 2)[0];
        var queryStart = withoutFragment.IndexOf(QueryStart, StringComparison.Ordinal);
        if (_addressSchemes.Any(scheme => withoutFragment.StartsWith(scheme, StringComparison.OrdinalIgnoreCase)))
            return (queryStart < 0 ? string.Empty : withoutFragment[(queryStart + 1)..], true);

        return queryStart == 0 ? (withoutFragment[1..], true) : (withoutFragment, false);
    }

    /// <summary>The named values of a query in their order, still escaped, as the parts of a pin are escaped one by one.</summary>
    private static List<(string Key, string Value)> ReadParameters(string query)
    {
        List<(string Key, string Value)> parameters = [];
        foreach (var part in query.Split(ParameterSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf(ValueSeparator, StringComparison.Ordinal);
            if (separator > 0)
                parameters.Add((Unescape(part[..separator]), part[(separator + 1)..]));
        }

        return parameters;
    }

    private static bool TryReadCoordinate(string text, out int coordinate)
    {
        var isNumber = TryReadNumber(text, out var number);
        coordinate = isNumber ? (int)Math.Clamp(Math.Floor(number), -TilePyramid.WorldLimitBlocks, TilePyramid.WorldLimitBlocks) : 0;
        return isNumber;
    }

    private static bool TryReadScale(string text, out double blocksPerPixel)
    {
        var isScale = TryReadNumber(text, out var number) && number > 0;
        blocksPerPixel = isScale ? Math.Clamp(number, TileMetrics.MinBlocksPerPixel, TileMetrics.Noise.MaxBlocksPerPixel) : 0;
        return isScale;
    }

    private static bool TryReadSampleHeight(string text, out int height)
    {
        var isHeight = int.TryParse(text, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number);
        height = Math.Clamp(number, Constants.MinSampleHeight, Constants.MaxSampleHeight);
        return isHeight;
    }

    private static bool TryReadOctaves(string text, out int octaves)
        => int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out octaves);

    private static bool TryReadNumber(string text, out double number)
        => double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out number) && double.IsFinite(number);

    /// <summary>A pin written as <c>x,z</c>, <c>x,z,name</c> or <c>x,z,name,look</c>, each part escaped on its own.</summary>
    private static bool TryReadPin(string text, [NotNullWhen(true)] out LinkPin? pin)
    {
        pin = null;
        var parts = text.Split(PinPartSeparator);
        if (parts.Length is < PinPositionParts or > PinParts
            || !TryReadCoordinate(Unescape(parts[0]), out var x) || !TryReadCoordinate(Unescape(parts[1]), out var z))
            return false;

        pin = new LinkPin(new BlockPos(x, z), ReadPinText(parts, PinNamePart), ReadPinText(parts, PinIconPart));
        return true;
    }

    private static string? ReadPinText(string[] parts, int index)
    {
        var text = index < parts.Length ? Unescape(parts[index]).Trim() : string.Empty;
        return text.Length == 0 ? null : text[..Math.Min(text.Length, MaxPinTextLength)];
    }

    private static string ToParameter(string key, string value) => $"{key}{ValueSeparator}{Uri.EscapeDataString(value)}";

    /// <summary>A pin as <c>pin=x,z,name,look</c>, leaving out a trailing name or look the pin does not have.</summary>
    private static string ToPinParameter(LinkPin pin)
    {
        string[] parts =
        [
            pin.Position.X.ToString(CultureInfo.InvariantCulture),
            pin.Position.Z.ToString(CultureInfo.InvariantCulture),
            pin.Name ?? string.Empty,
            pin.Icon ?? string.Empty,
        ];
        var count = pin.Icon is not null ? PinParts : pin.Name is not null ? PinIconPart : PinPositionParts;
        return $"{PinKey}{ValueSeparator}{string.Join(PinPartSeparator, parts.Take(count).Select(Uri.EscapeDataString))}";
    }

    private static string Unescape(string text) => Uri.UnescapeDataString(text.Replace(EscapedSpace, ' '));

    private static FrozenDictionary<string, TEnum> CreateNames<TEnum>() where TEnum : struct, Enum
        => Enum.GetValues<TEnum>().ToFrozenDictionary(ToName, StringComparer.OrdinalIgnoreCase);

    private static string ToName<TEnum>(TEnum value) where TEnum : struct, Enum => value.ToString().ToLowerInvariant();

    private delegate bool ValueReader<T>(string text, out T value);

    /// <summary>
    /// Reads the values of a link and remembers those it could not read. A name given more than once keeps its first value, except
    /// for pins, which a link names once for each pin.
    /// </summary>
    private sealed class ParameterReader
    {
        private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);
        private readonly List<string> _pins = [];
        private readonly List<string> _unreadable = [];
        private int _unreadablePins;
        private bool _hasMorePins;

        public ParameterReader(IEnumerable<(string Key, string Value)> parameters)
        {
            foreach (var (key, value) in parameters)
            {
                if (string.Equals(key, PinKey, StringComparison.OrdinalIgnoreCase))
                    _pins.Add(value);
                else
                    _values.TryAdd(key, value);
            }
        }

        public string? Problem
        {
            get
            {
                string[] problems = [.. new[] { DescribeUnreadableValues(), DescribeUnreadablePins(), DescribeLeftOutPins() }.OfType<string>()];
                return problems.Length == 0 ? null : string.Join(' ', problems);
            }
        }

        public bool TryGetText(string key, [NotNullWhen(true)] out string? text)
        {
            text = _values.TryGetValue(key, out var escaped) ? Unescape(escaped) : null;
            return text is not null;
        }

        /// <summary>A link's value: <paramref name="missing"/> where it leaves the value out, <paramref name="unreadable"/> where it can't be read.</summary>
        public T Read<T>(string key, ValueReader<T> reader, T missing, T unreadable)
        {
            if (!TryGetText(key, out var text))
                return missing;
            if (reader(text, out var value))
                return value;

            _unreadable.Add(key);
            return unreadable;
        }

        /// <summary>The pins of the link in their order, up to <see cref="MaxPins"/>, leaving out those that can't be read.</summary>
        public LinkPin[] ReadPins()
        {
            List<LinkPin> pins = [];
            foreach (var text in _pins)
            {
                if (pins.Count == MaxPins)
                {
                    _hasMorePins = true;
                    break;
                }

                if (TryReadPin(text, out var pin))
                    pins.Add(pin);
                else
                    _unreadablePins++;
            }

            return [.. pins];
        }

        private string? DescribeUnreadableValues() => _unreadable.Count switch
        {
            0 => null,
            1 => $"The link's {_unreadable[0]} could not be read and stays as it is.",
            _ => $"The link's {string.Join(", ", _unreadable.Take(_unreadable.Count - 1))} and {_unreadable[^1]} could not be read and stay as they are.",
        };

        private string? DescribeUnreadablePins() => _unreadablePins switch
        {
            0 => null,
            1 => "One of the link's pins could not be read and was left out.",
            _ => string.Create(CultureInfo.InvariantCulture, $"{_unreadablePins} of the link's pins could not be read and were left out."),
        };

        private string? DescribeLeftOutPins()
            => _hasMorePins ? string.Create(CultureInfo.InvariantCulture, $"The link has more than {MaxPins} pins; only the first {MaxPins} are added.") : null;
    }
}

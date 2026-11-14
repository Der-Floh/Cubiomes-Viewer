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
    private const string ScaleFormat = "0.#####";
    private const string TrueFlag = "1";
    private const string FalseFlag = "0";
    private const char QueryStart = '?';
    private const char FragmentStart = '#';
    private const char ParameterSeparator = '&';
    private const char ValueSeparator = '=';
    private const char EscapedSpace = '+';
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
        var parameters = ReadParameters(query);
        if (!parameters.TryGetValue(SeedKey, out var seed) || string.IsNullOrWhiteSpace(seed))
        {
            problem = isAddress ? NoSeedProblem : null;
            return false;
        }

        var reader = new ParameterReader(parameters);
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
            reader.Read(OctavesKey, TryReadOctaves, 0, current.Octaves));
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

    /// <summary>The named values of a query; a name given twice keeps its first value.</summary>
    private static Dictionary<string, string> ReadParameters(string query)
    {
        Dictionary<string, string> parameters = new(StringComparer.OrdinalIgnoreCase);
        foreach (var part in query.Split(ParameterSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            var separator = part.IndexOf(ValueSeparator, StringComparison.Ordinal);
            if (separator > 0)
                parameters.TryAdd(Unescape(part[..separator]), Unescape(part[(separator + 1)..]));
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

    private static string ToParameter(string key, string value) => $"{key}{ValueSeparator}{Uri.EscapeDataString(value)}";

    private static string Unescape(string text) => Uri.UnescapeDataString(text.Replace(EscapedSpace, ' '));

    private static FrozenDictionary<string, TEnum> CreateNames<TEnum>() where TEnum : struct, Enum
        => Enum.GetValues<TEnum>().ToFrozenDictionary(ToName, StringComparer.OrdinalIgnoreCase);

    private static string ToName<TEnum>(TEnum value) where TEnum : struct, Enum => value.ToString().ToLowerInvariant();

    private delegate bool ValueReader<T>(string text, out T value);

    /// <summary>Reads the values of a link and remembers the names of those it could not read.</summary>
    private sealed class ParameterReader(Dictionary<string, string> parameters)
    {
        private readonly List<string> _unreadable = [];

        public string? Problem => _unreadable.Count switch
        {
            0 => null,
            1 => $"The link's {_unreadable[0]} could not be read and stays as it is.",
            _ => $"The link's {string.Join(", ", _unreadable.Take(_unreadable.Count - 1))} and {_unreadable[^1]} could not be read and stay as they are.",
        };

        /// <summary>A link's value: <paramref name="missing"/> where it leaves the value out, <paramref name="unreadable"/> where it can't be read.</summary>
        public T Read<T>(string key, ValueReader<T> reader, T missing, T unreadable)
        {
            if (!parameters.TryGetValue(key, out var text))
                return missing;
            if (reader(text, out var value))
                return value;

            _unreadable.Add(key);
            return unreadable;
        }
    }
}

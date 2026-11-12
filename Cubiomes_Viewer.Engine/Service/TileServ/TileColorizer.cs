using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.LayerServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.TileServ;

public sealed class TileColorizer : ITileColorizer
{
    public const int BytesPerPixel = 4;

    private const byte Opaque = byte.MaxValue;
    private const int StoredNumberCount = ushort.MaxValue + 1;

    private readonly IBiomeCatalog _biomeCatalog;
    private readonly ILayerCatalog _layerCatalog;
    private readonly IStructureFinder _structureFinder;
    private readonly ConcurrentDictionary<(ColorRamp Ramp, int Unit), RgbColor[]> _colorTables = new();
    private readonly ConditionalWeakTable<BiomePalette, ConcurrentDictionary<(StructureType Type, McVersion Version), RgbColor[]>> _potentialColors = new();

    public TileColorizer(IBiomeCatalog biomeCatalog, ILayerCatalog layerCatalog, IStructureFinder structureFinder)
    {
        _biomeCatalog = biomeCatalog;
        _layerCatalog = layerCatalog;
        _structureFinder = structureFinder;
    }

    public void Colorize(TileData tile, TileStyle style, Span<byte> bgra)
    {
        ArgumentNullException.ThrowIfNull(tile);

        var samples = tile.HasValues ? tile.Values.Length : tile.Biomes.Length;
        if (bgra.Length < samples * BytesPerPixel)
            throw new ArgumentException($"The pixel buffer holds {bgra.Length} bytes but {samples} samples need {samples * BytesPerPixel}.", nameof(bgra));

        if (!tile.HasValues)
        {
            ColorizeBiomes(tile.Biomes, GetBiomeColors(style, tile.Request.World.Version), bgra);
            return;
        }

        var layer = GetValues(tile.Request);
        ColorizeValues(tile.Values, layer.GetRamp(style), layer.Unit, bgra);
        if (tile.Request.Layer == LayerMode.Height)
            AddRelief(tile, style.Height, layer.Unit, bgra);
    }

    /// <summary>The palette of the style, or that palette with every biome dimmed in which the structure type cannot generate.</summary>
    public IReadOnlyList<RgbColor> GetBiomeColors(TileStyle style, McVersion version)
    {
        var palette = style.Colors ?? _biomeCatalog.DefaultPalette;
        if (style.PotentialOf is not { } type)
            return palette.Colors;

        return _potentialColors.GetValue(palette, static _ => new()).GetOrAdd((type, version), key => _structureFinder.TryGetViableBiomes(key.Type, key.Version, out var viable)
            ? [.. Enumerable.Range(0, palette.Count).Select(biome => viable.Contains(biome) ? palette[biome] : palette[biome].ToDimmed())]
            : [.. palette.Colors]);
    }

    private static void AddRelief(TileData tile, HeightStyle style, int unit, Span<byte> bgra)
    {
        if (style == HeightStyle.Shaded)
            HeightRelief.Shade(tile.Values, tile.Samples, tile.Request.Tile.Scale, unit, bgra);
        else if (style == HeightStyle.Contours)
            HeightRelief.DrawContours(tile.Values, tile.Samples, tile.Request.Tile.Scale, unit, bgra);
    }

    private LayerValues GetValues(TileRequest request)
        => _layerCatalog.GetInfo(request.Layer, request.World.Version, request.World.Dimension).Values
            ?? throw new InvalidOperationException($"The layer '{request.Layer}' has no colours for numbers.");

    private static void ColorizeBiomes(ReadOnlySpan<byte> biomes, IReadOnlyList<RgbColor> colors, Span<byte> bgra)
    {
        for (var index = 0; index < biomes.Length; index++)
        {
            var pixel = bgra.Slice(index * BytesPerPixel, BytesPerPixel);
            if (biomes[index] == Constants.NoBiomeTileValue)
                pixel.Clear();
            else
                Write(pixel, colors[biomes[index]]);
        }
    }

    private void ColorizeValues(ReadOnlySpan<short> values, ColorRamp ramp, int unit, Span<byte> bgra)
    {
        var table = _colorTables.GetOrAdd((ramp, unit), CreateColorTable);
        for (var index = 0; index < values.Length; index++)
            Write(bgra.Slice(index * BytesPerPixel, BytesPerPixel), table[(ushort)values[index]]);
    }

    /// <summary>The colour of every number a sample can hold, at the index its bits give as an unsigned number.</summary>
    private static RgbColor[] CreateColorTable((ColorRamp Ramp, int Unit) colors)
    {
        var table = new RgbColor[StoredNumberCount];
        for (int stored = short.MinValue; stored <= short.MaxValue; stored++)
            table[(ushort)stored] = colors.Ramp.GetColor(stored / (double)colors.Unit);
        return table;
    }

    private static void Write(Span<byte> pixel, RgbColor color)
    {
        pixel[0] = color.Blue;
        pixel[1] = color.Green;
        pixel[2] = color.Red;
        pixel[3] = Opaque;
    }
}

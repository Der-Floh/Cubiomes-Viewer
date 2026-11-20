using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.LayerServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.TileServ;

/// <summary>
/// Paints tiles in the colours of their biomes or, for layers of numbers, in the steps of their colour ramp: 256 steps of
/// equal colour change, 128 where contour lines take a darker copy of each step, so that every such layer fits a palette of
/// 256 colours. Grey ramps keep every shade they have.
/// </summary>
public sealed class TileColorizer : ITileColorizer
{
    public const int BytesPerPixel = 4;

    private const byte Opaque = byte.MaxValue;
    private const int StoredNumberCount = ushort.MaxValue + 1;
    private const int RampSteps = RampPalette.MaxSteps;
    private const int ContourRampSteps = RampSteps / 2;

    private readonly IBiomeCatalog _biomeCatalog;
    private readonly ILayerCatalog _layerCatalog;
    private readonly IStructureFinder _structureFinder;
    private readonly ConcurrentDictionary<(ColorRamp Ramp, int Unit, int Steps), RampPalette> _rampPalettes = new();
    private readonly ConcurrentDictionary<(ColorRamp Ramp, int Unit, int Steps), RgbColor[]> _colorTables = new();
    private readonly ConditionalWeakTable<BiomePalette, ConcurrentDictionary<(StructureType Type, McVersion Version), RgbColor[]>> _potentialColors = new();

    public TileColorizer(IBiomeCatalog biomeCatalog, ILayerCatalog layerCatalog, IStructureFinder structureFinder)
    {
        _biomeCatalog = biomeCatalog;
        _layerCatalog = layerCatalog;
        _structureFinder = structureFinder;
    }

    public void Colorize(TileData tile, TileStyle style, Span<byte> bgra, TileData? heights = null)
    {
        ArgumentNullException.ThrowIfNull(tile);

        var samples = tile.HasValues ? tile.Samples * tile.Samples : tile.Biomes.Length;
        if (bgra.Length < samples * BytesPerPixel)
            throw new ArgumentException($"The pixel buffer holds {bgra.Length} bytes but {samples} samples need {samples * BytesPerPixel}.", nameof(bgra));
        if (tile.HasValues && tile.Values.Length != tile.Stride * tile.Stride)
            throw new ArgumentException($"The tile {tile} holds {tile.Values.Length} numbers instead of {tile.Stride * tile.Stride}.", nameof(tile));
        if (heights is not null && (!heights.HasValues || heights.Values.Length != heights.Stride * heights.Stride || heights.Request != GetReliefTile(tile.Request, style)))
            throw new ArgumentException($"The tile {heights} does not hold the heights that shade the tile {tile} in this style.", nameof(heights));

        if (!tile.HasValues)
        {
            ColorizeBiomes(tile.Biomes, GetBiomeColors(style, tile.Request.World.Version), bgra);
            if (heights is not null)
                AddBiomeRelief(tile, heights, bgra);
            return;
        }

        var layer = GetValues(tile.Request);
        ColorizeValues(tile, GetColorTable(layer.GetRamp(style), layer.Unit, GetRampSteps(tile.Request, style)), bgra);
        if (tile.Request.Layer == LayerMode.Height)
            AddRelief(tile, style.Height, layer.Unit, bgra);
    }

    /// <summary>
    /// The steps of the ramp of a layer of numbers in a style, followed by their darker copies where contour lines take them;
    /// <see langword="null"/> for biomes, greys, and shaded heights, whose light and shade make more colours than that.
    /// </summary>
    public IReadOnlyList<RgbColor>? GetPalette(TileRequest tile, TileStyle style)
    {
        if (tile.Layer.HasBiomeSamples)
            return null;

        var layer = GetValues(tile);
        var ramp = layer.GetRamp(style);
        if (ramp.IsGray || (tile.Layer == LayerMode.Height && style.Height == HeightStyle.Shaded))
            return null;

        var steps = GetRampPalette(ramp, layer.Unit, GetRampSteps(tile, style)).Colors;
        return DrawsContours(tile, style) ? [.. steps, .. steps.Select(HeightRelief.DarkenForContour)] : steps;
    }

    public TileRequest? GetReliefTile(TileRequest tile, TileStyle style)
    {
        if (!style.BiomeRelief || !tile.Layer.ShowsBiomeMap)
            return null;

        var world = tile.World;
        if (!_layerCatalog.IsAvailable(LayerMode.Height, world.Version, world.Dimension))
            return null;

        return TilePyramid.FindCoveringTile(TileMetrics.For(world, tile.Layer), tile.Tile, TileMetrics.For(world, LayerMode.Height)) is { } key
            ? MapContent.For(world, LayerMode.Height, 0, 0).GetRequest(key)
            : null;
    }

    /// <summary>Layers of numbers painted with a grey ramp stay grey, as the light and shade of their relief scales red, green and blue alike.</summary>
    public bool PaintsGray(TileRequest tile, TileStyle style)
        => !tile.Layer.HasBiomeSamples && GetValues(tile).GetRamp(style).IsGray;

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
            HeightRelief.Shade(tile, unit, bgra);
        else if (style == HeightStyle.Contours)
            HeightRelief.DrawContours(tile, unit, bgra);
    }

    private void AddBiomeRelief(TileData tile, TileData heights, Span<byte> bgra)
    {
        if (tile.Biomes.Length != tile.Samples * tile.Samples)
            throw new ArgumentException($"The tile {tile} holds {tile.Biomes.Length} biomes instead of {tile.Samples * tile.Samples}.", nameof(tile));

        var covered = TilePyramid.GetCoveredSamples(tile.Request.Tile, tile.Samples, heights.Request.Tile, heights.Samples);
        HeightRelief.ShadeCovered(heights, GetValues(heights.Request).Unit, covered, tile.Samples, bgra);
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

    /// <summary>Paints the tile's own samples; the border it keeps of its neighbours' only feeds the relief.</summary>
    private static void ColorizeValues(TileData tile, RgbColor[] table, Span<byte> bgra)
    {
        for (var z = 0; z < tile.Samples; z++)
        {
            for (var x = 0; x < tile.Samples; x++)
                Write(bgra.Slice((z * tile.Samples + x) * BytesPerPixel, BytesPerPixel), table[(ushort)tile.GetValue(x, z)]);
        }
    }

    private static int GetRampSteps(TileRequest tile, TileStyle style) => DrawsContours(tile, style) ? ContourRampSteps : RampSteps;

    private static bool DrawsContours(TileRequest tile, TileStyle style) => tile.Layer == LayerMode.Height && style.Height == HeightStyle.Contours;

    private RampPalette GetRampPalette(ColorRamp ramp, int unit, int steps)
        => _rampPalettes.GetOrAdd((ramp, unit, steps), static key => RampPalette.Create(key.Ramp, key.Unit, key.Steps));

    /// <summary>The colour of every number a sample can hold, at the index its bits give as an unsigned number: the step it falls in, or for a grey ramp its own shade.</summary>
    private RgbColor[] GetColorTable(ColorRamp ramp, int unit, int steps) => _colorTables.GetOrAdd((ramp, unit, steps), key =>
    {
        var table = new RgbColor[StoredNumberCount];
        var palette = key.Ramp.IsGray ? null : GetRampPalette(key.Ramp, key.Unit, key.Steps);
        for (int stored = short.MinValue; stored <= short.MaxValue; stored++)
            table[(ushort)stored] = palette?.GetColor((short)stored) ?? key.Ramp.GetColor(stored / (double)key.Unit);
        return table;
    });

    private static void Write(Span<byte> pixel, RgbColor color)
    {
        pixel[0] = color.Blue;
        pixel[1] = color.Green;
        pixel[2] = color.Red;
        pixel[3] = Opaque;
    }
}

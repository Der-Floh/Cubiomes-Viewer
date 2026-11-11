using System.Collections.Concurrent;

using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Service.LayerServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.TileServ;

public sealed class TileColorizer : ITileColorizer
{
    public const int BytesPerPixel = 4;

    private const byte Opaque = byte.MaxValue;
    private const int StoredNumberCount = ushort.MaxValue + 1;

    private readonly IBiomeCatalog _biomeCatalog;
    private readonly ILayerCatalog _layerCatalog;
    private readonly ConcurrentDictionary<LayerValues, RgbColor[]> _colorTables = new();

    public TileColorizer(IBiomeCatalog biomeCatalog, ILayerCatalog layerCatalog)
    {
        _biomeCatalog = biomeCatalog;
        _layerCatalog = layerCatalog;
    }

    public void Colorize(TileData tile, Span<byte> bgra)
    {
        ArgumentNullException.ThrowIfNull(tile);

        var samples = tile.HasValues ? tile.Values.Length : tile.Biomes.Length;
        if (bgra.Length < samples * BytesPerPixel)
            throw new ArgumentException($"The pixel buffer holds {bgra.Length} bytes but {samples} samples need {samples * BytesPerPixel}.", nameof(bgra));

        if (tile.HasValues)
            ColorizeValues(tile.Values, GetValues(tile.Request), bgra);
        else
            ColorizeBiomes(tile.Biomes, _biomeCatalog.DefaultPalette, bgra);
    }

    private LayerValues GetValues(TileRequest request)
        => _layerCatalog.GetInfo(request.Layer, request.World.Version, request.World.Dimension).Values
            ?? throw new InvalidOperationException($"The layer '{request.Layer}' has no colours for numbers.");

    private static void ColorizeBiomes(ReadOnlySpan<byte> biomes, BiomePalette palette, Span<byte> bgra)
    {
        for (var index = 0; index < biomes.Length; index++)
        {
            var pixel = bgra.Slice(index * BytesPerPixel, BytesPerPixel);
            if (biomes[index] == Constants.NoBiomeTileValue)
                pixel.Clear();
            else
                Write(pixel, palette[biomes[index]]);
        }
    }

    private void ColorizeValues(ReadOnlySpan<short> values, LayerValues layer, Span<byte> bgra)
    {
        var table = _colorTables.GetOrAdd(layer, CreateColorTable);
        for (var index = 0; index < values.Length; index++)
            Write(bgra.Slice(index * BytesPerPixel, BytesPerPixel), table[(ushort)values[index]]);
    }

    /// <summary>The colour of every number a sample can hold, at the index its bits give as an unsigned number.</summary>
    private static RgbColor[] CreateColorTable(LayerValues layer)
    {
        var table = new RgbColor[StoredNumberCount];
        for (int stored = short.MinValue; stored <= short.MaxValue; stored++)
            table[(ushort)stored] = layer.Ramp.GetColor(layer.ToValue((short)stored));
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

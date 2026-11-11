using Cubiomes_Viewer.Engine.Data.Colors;

namespace Cubiomes_Viewer.Engine.Data.Biomes;

/// <summary>One colour per biome id (0–255).</summary>
public sealed class BiomePalette
{
    public int Count => _colors.Length;
    public RgbColor this[int biome] => _colors[biome];

    private readonly RgbColor[] _colors;

    private BiomePalette(RgbColor[] colors)
    {
        _colors = colors;
    }

    public static BiomePalette FromRgb(ReadOnlySpan<byte> rgb)
    {
        if (rgb.Length != Constants.BiomeColorCount * Constants.BiomeColorChannels)
            throw new ArgumentException($"A palette needs {Constants.BiomeColorCount} RGB triples but {rgb.Length} bytes were given.", nameof(rgb));

        var colors = new RgbColor[Constants.BiomeColorCount];
        for (var biome = 0; biome < colors.Length; biome++)
        {
            var offset = biome * Constants.BiomeColorChannels;
            colors[biome] = new RgbColor(rgb[offset], rgb[offset + 1], rgb[offset + 2]);
        }

        return new BiomePalette(colors);
    }
}

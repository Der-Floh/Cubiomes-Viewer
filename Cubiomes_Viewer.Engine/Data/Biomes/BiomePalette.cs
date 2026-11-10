namespace Cubiomes_Viewer.Engine.Data.Biomes;

/// <summary>One colour per biome id (0–255).</summary>
public sealed class BiomePalette
{
    public int Count => _colors.Length;
    public BiomeColor this[int biome] => _colors[biome];

    private readonly BiomeColor[] _colors;

    private BiomePalette(BiomeColor[] colors)
    {
        _colors = colors;
    }

    public static BiomePalette FromRgb(ReadOnlySpan<byte> rgb)
    {
        if (rgb.Length != Constants.BiomeColorCount * Constants.BiomeColorChannels)
            throw new ArgumentException($"A palette needs {Constants.BiomeColorCount} RGB triples but {rgb.Length} bytes were given.", nameof(rgb));

        var colors = new BiomeColor[Constants.BiomeColorCount];
        for (var biome = 0; biome < colors.Length; biome++)
        {
            var offset = biome * Constants.BiomeColorChannels;
            colors[biome] = new BiomeColor(rgb[offset], rgb[offset + 1], rgb[offset + 2]);
        }

        return new BiomePalette(colors);
    }
}

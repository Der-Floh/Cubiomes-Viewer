using System.Text.Json.Serialization;

namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>The samples of one tile, row by row: biome ids for a layer of biomes, numbers for any other layer.</summary>
public sealed class TileData
{
    public required TileRequest Request { get; init; }

    /// <summary>Samples per tile side.</summary>
    public required int Samples { get; init; }

    /// <summary>
    /// The samples of the neighbouring tiles that <see cref="Values"/> keeps around the tile's own on each side; height tiles
    /// keep some, so that slopes can be measured right up to their edges.
    /// </summary>
    public int Border { get; init; }

    /// <summary>One biome id per sample; <c>255</c> marks a sample without a biome. Empty for a layer of numbers.</summary>
    public byte[] Biomes { get; init; } = [];

    /// <summary>One number per sample in the steps of the layer, the border included. Empty for a layer of biomes.</summary>
    [JsonConverter(typeof(Int16ArrayJsonConverter))]
    public short[] Values { get; init; } = [];

    [JsonIgnore]
    public bool HasValues => Values.Length > 0;

    /// <summary>The numbers in one row of <see cref="Values"/>, the border included.</summary>
    [JsonIgnore]
    public int Stride => Samples + 2 * Border;

    [JsonIgnore]
    public int ByteSize => Biomes.Length + Values.Length * sizeof(short);

    public byte GetBiome(int sampleX, int sampleZ) => Biomes[sampleZ * Samples + sampleX];

    /// <summary>The number of one of the tile's own samples; coordinates below zero or from <see cref="Samples"/> on reach into the border.</summary>
    public short GetValue(int sampleX, int sampleZ) => Values[(sampleZ + Border) * Stride + sampleX + Border];

    public override string ToString() => $"{Request.Tile} ({Samples}x{Samples})";
}

using System.Text.Json.Serialization;

namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>The samples of one tile, row by row: biome ids for a layer of biomes, numbers for any other layer.</summary>
public sealed class TileData
{
    public required TileRequest Request { get; init; }

    /// <summary>Samples per tile side.</summary>
    public required int Samples { get; init; }

    /// <summary>One biome id per sample; <c>255</c> marks a sample without a biome. Empty for a layer of numbers.</summary>
    public byte[] Biomes { get; init; } = [];

    /// <summary>One number per sample in the steps of the layer. Empty for a layer of biomes.</summary>
    [JsonConverter(typeof(Int16ArrayJsonConverter))]
    public short[] Values { get; init; } = [];

    [JsonIgnore]
    public bool HasValues => Values.Length > 0;

    [JsonIgnore]
    public int ByteSize => Biomes.Length + Values.Length * sizeof(short);

    public byte GetBiome(int sampleX, int sampleZ) => Biomes[sampleZ * Samples + sampleX];

    public short GetValue(int sampleX, int sampleZ) => Values[sampleZ * Samples + sampleX];

    public override string ToString() => $"{Request.Tile} ({Samples}x{Samples})";
}

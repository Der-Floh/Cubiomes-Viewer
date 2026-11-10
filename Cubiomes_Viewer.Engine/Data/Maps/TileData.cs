using System.Text.Json.Serialization;

namespace Cubiomes_Viewer.Engine.Data.Maps;

public sealed class TileData
{
    public required TileRequest Request { get; init; }

    /// <summary>Samples per tile side.</summary>
    public required int Samples { get; init; }

    /// <summary>One biome id per sample, row by row; <c>255</c> marks a sample without a biome.</summary>
    public required byte[] Biomes { get; init; }

    [JsonIgnore]
    public int ByteSize => Biomes.Length;

    public byte GetBiome(int sampleX, int sampleZ) => Biomes[sampleZ * Samples + sampleX];

    public override string ToString() => $"{Request.Tile} ({Samples}x{Samples})";
}

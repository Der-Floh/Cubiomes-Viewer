using System.Text.Json.Serialization;

namespace Cubiomes_Viewer.Engine.Data.Colors;

/// <summary>The colour a biome id is painted in.</summary>
public readonly record struct BiomeColor(int Biome, [property: JsonConverter(typeof(RgbColorHexJsonConverter))] RgbColor Color);

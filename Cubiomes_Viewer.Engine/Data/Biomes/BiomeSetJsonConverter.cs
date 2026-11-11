using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cubiomes_Viewer.Engine.Data.Biomes;

/// <summary>Writes a set of biomes as the array of its ids.</summary>
public sealed class BiomeSetJsonConverter : JsonConverter<BiomeSet>
{
    public override BiomeSet Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.StartArray)
            throw new JsonException("A set of biomes is written as an array of biome ids.");

        var set = BiomeSet.Empty;
        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
        {
            var biome = reader.GetInt32();
            if (biome is < 0 or > BiomeSet.MaxBiome)
                throw new JsonException($"{biome} is not a biome id between 0 and {BiomeSet.MaxBiome}.");
            set = set.With(biome);
        }

        return set;
    }

    public override void Write(Utf8JsonWriter writer, BiomeSet value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);

        writer.WriteStartArray();
        foreach (var biome in value.ToArray())
            writer.WriteNumberValue(biome);
        writer.WriteEndArray();
    }
}

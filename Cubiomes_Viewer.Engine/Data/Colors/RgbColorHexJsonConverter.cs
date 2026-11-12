using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cubiomes_Viewer.Engine.Data.Colors;

/// <summary>Writes a colour as <c>#rrggbb</c>, the way people write colours by hand.</summary>
public sealed class RgbColorHexJsonConverter : JsonConverter<RgbColor>
{
    public override RgbColor Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        => RgbColor.TryParseHex(reader.GetString(), out var color)
            ? color
            : throw new JsonException($"'{reader.GetString()}' is no colour like #8db360.");

    public override void Write(Utf8JsonWriter writer, RgbColor value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        writer.WriteStringValue(value.ToHex());
    }
}

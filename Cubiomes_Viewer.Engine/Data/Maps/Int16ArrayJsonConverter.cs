using System.Buffers.Binary;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>
/// Writes an array of 16-bit numbers as Base64 of their little-endian bytes, which is far smaller and faster
/// than a JSON array of numbers for the thousands of samples of a tile.
/// </summary>
public sealed class Int16ArrayJsonConverter : JsonConverter<short[]>
{
    public override short[] Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var bytes = reader.GetBytesFromBase64();
        if (bytes.Length % sizeof(short) != 0)
            throw new JsonException($"{bytes.Length} bytes do not make whole 16-bit numbers.");

        var values = new short[bytes.Length / sizeof(short)];
        for (var index = 0; index < values.Length; index++)
            values[index] = BinaryPrimitives.ReadInt16LittleEndian(bytes.AsSpan(index * sizeof(short)));
        return values;
    }

    public override void Write(Utf8JsonWriter writer, short[] value, JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(writer);
        ArgumentNullException.ThrowIfNull(value);

        var bytes = new byte[value.Length * sizeof(short)];
        for (var index = 0; index < value.Length; index++)
            BinaryPrimitives.WriteInt16LittleEndian(bytes.AsSpan(index * sizeof(short)), value[index]);
        writer.WriteBase64StringValue(bytes);
    }
}

using System.Text.Json;

using Cubiomes_Viewer.Engine.Data.Maps;

namespace Cubiomes_Viewer.Engine.Tests.Data;

public sealed class Int16ArrayJsonConverterTests
{
    private static readonly JsonSerializerOptions _options = new() { Converters = { new Int16ArrayJsonConverter() } };

    [Fact]
    public void Serialize_Numbers_WritesTheirLittleEndianBytesAsBase64()
        => Assert.Equal("\"AQD+/w==\"", JsonSerializer.Serialize<short[]>([1, -2], _options));

    [Fact]
    public void Deserialize_SerializedNumbers_RestoresThem()
    {
        short[] values = [short.MinValue, -1, 0, 1, 12345, short.MaxValue];

        var restored = JsonSerializer.Deserialize<short[]>(JsonSerializer.Serialize(values, _options), _options);

        Assert.Equal(values, restored);
    }

    [Fact]
    public void Deserialize_NoNumbers_GivesAnEmptyArray()
        => Assert.Empty(JsonSerializer.Deserialize<short[]>("\"\"", _options) ?? [0]);

    [Fact]
    public void Deserialize_HalfANumber_IsRejected()
        => Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<short[]>("\"AQD+\"", _options));
}

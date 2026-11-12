using System.Text.Json.Serialization;

using Cubiomes_Viewer.Engine.Data.Colors;

namespace Cubiomes_Viewer.Engine.Repository.ColorMapRepo;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(BiomeColorMapFile))]
internal sealed partial class ColorMapJsonContext : JsonSerializerContext;

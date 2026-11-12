using System.Text.Json.Serialization;

using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Repository.PresetRepo;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(SearchPresetList))]
internal sealed partial class PresetJsonContext : JsonSerializerContext;

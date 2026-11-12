using System.Text.Json.Serialization;

using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Repository.SessionRepo;

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(SearchSession))]
internal sealed partial class SessionJsonContext : JsonSerializerContext;

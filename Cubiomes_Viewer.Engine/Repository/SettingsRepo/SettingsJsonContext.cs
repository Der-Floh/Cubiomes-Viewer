using System.Text.Json.Serialization;

using Cubiomes_Viewer.Engine.Data.Settings;

namespace Cubiomes_Viewer.Engine.Repository.SettingsRepo;

[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    UseStringEnumConverter = true)]
[JsonSerializable(typeof(AppSettings))]
internal sealed partial class SettingsJsonContext : JsonSerializerContext;

using System.Globalization;

using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Repository.SessionRepo;

using Microsoft.Extensions.Logging.Abstractions;

namespace Cubiomes_Viewer.Cli.Tests;

/// <summary>
/// Session files as the app saves them: example presets of the app cut down to a few seeds, written by the engine's
/// session repository. They cover seed ranges and structure seeds, structures, biomes, the spawn, strongholds and
/// slime chunks.
/// </summary>
internal static class FixtureSessions
{
    public const string SlimeChunks = "slime-chunks";

    private const string Folder = "Fixtures";
    private const string Extension = ".json";

    public static IReadOnlyList<string> Names { get; } = [SlimeChunks, "village-by-spawn", "mushroom-fields", "four-villages", "end-ship", "stronghold"];

    private static readonly SearchSessionRepository _repository = new(new FileSystemAppDataStore(Path.GetTempPath()), NullLogger<SearchSessionRepository>.Instance);

    public static string GetPath(string name) => Path.Combine(AppContext.BaseDirectory, Folder, name + Extension);

    /// <summary>Copies a fixture into <paramref name="directory"/>, since the CLI saves its progress into the session file.</summary>
    public static string CopyTo(string name, string directory)
    {
        var path = Path.Combine(directory, name + Extension);
        File.Copy(GetPath(name), path, overwrite: true);
        return path;
    }

    public static SearchSession Read(string path) => _repository.Deserialize(File.ReadAllBytes(path));

    public static byte[] Serialize(SearchSession session) => _repository.Serialize(session);

    /// <summary>Writes a session file into <paramref name="directory"/> as the app would save it.</summary>
    public static string Write(SearchSession session, string directory)
    {
        var path = Path.Combine(directory, Guid.NewGuid().ToString("N") + Extension);
        File.WriteAllBytes(path, Serialize(session));
        return path;
    }

    public static IReadOnlyList<long> ParseSeeds(string text)
        => [.. text.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(line => long.Parse(line, CultureInfo.InvariantCulture))];
}

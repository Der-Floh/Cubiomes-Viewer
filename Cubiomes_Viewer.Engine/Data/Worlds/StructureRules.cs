using System.Globalization;
using System.Text.Json.Serialization;

namespace Cubiomes_Viewer.Engine.Data.Worlds;

/// <summary>
/// How structures generate in a world where it differs from the game: salts replaced per structure type, as servers
/// and data packs can set them, and whether an estimate of the terrain rules structures out. Rules with the same
/// content are equal.
/// </summary>
public sealed record StructureRules
{
    public static StructureRules Default { get; } = new();

    /// <summary>
    /// Whether the desert pyramids, jungle temples and woodland mansions of 1.18 and later are left out where an
    /// estimate puts the land too low for them.
    /// </summary>
    public bool EstimateTerrain { get; init; } = true;

    /// <summary>The replaced salts in the order of the structure types; a later salt for the same type wins.</summary>
    public IReadOnlyList<StructureSalt> Salts
    {
        get;
        init => field = [.. (value ?? []).GroupBy(salt => salt.Type).Select(group => group.Last()).OrderBy(salt => salt.Type)];
    } = [];

    [JsonIgnore]
    public bool IsDefault => EstimateTerrain && Salts.Count == 0;

    /// <summary>Gets the salt that replaces the game's for a structure type; returns <see langword="false"/> when there is none.</summary>
    public bool TryGetSalt(StructureType type, out int salt)
    {
        foreach (var replaced in Salts)
        {
            if (replaced.Type == type)
            {
                salt = replaced.Salt;
                return true;
            }
        }

        salt = 0;
        return false;
    }

    public bool Equals(StructureRules? other)
        => other is not null && EstimateTerrain == other.EstimateTerrain && Salts.SequenceEqual(other.Salts);

    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(EstimateTerrain);
        foreach (var salt in Salts)
            hash.Add(salt);
        return hash.ToHashCode();
    }

    public override string ToString()
    {
        if (IsDefault)
            return "the game's structure rules";

        var salts = string.Join(", ", Salts.Select(salt => string.Create(CultureInfo.InvariantCulture, $"{salt.Type} {salt.Salt}")));
        return EstimateTerrain ? $"salts {salts}" : $"no terrain estimate, salts {salts}";
    }
}

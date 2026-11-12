using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

/// <summary>Quads of swamp huts and the other structures placed like them: desert pyramids, jungle temples and igloos.</summary>
public interface IQuadFinder
{
    /// <summary>Whether a structure type forms quads in a version.</summary>
    bool SupportsQuads(StructureType type, McVersion version);

    /// <summary>The lower 20 bits that the quad-hut bases of a class have, from cubiomes' lists of constellations.</summary>
    IReadOnlyList<ulong> GetLowBits(QuadQuality quality);

    /// <summary>
    /// Writes the quad-hut bases among the values with the lower 20 bits <paramref name="lowBits"/> and the next 28
    /// bits from <paramref name="firstHigh"/> on, <paramref name="count"/> of them, and returns their number.
    /// </summary>
    int ScanQuadHutBases(ulong lowBits, uint firstHigh, uint count, Span<ulong> destination);

    /// <summary>Describes a quad-hut base, or returns <see langword="null"/> when the value is none.</summary>
    QuadHutBase? DescribeQuadHutBase(ulong value);

    /// <summary>
    /// The lower 48 bits of the seeds whose quad of the base lies in the regions from (<paramref name="regionX"/>,
    /// <paramref name="regionZ"/>) on, with the salt the rules give the structure type.
    /// </summary>
    long GetSeedOfBase(StructureType type, McVersion version, StructureRules rules, ulong value, int regionX, int regionZ);

    /// <summary>
    /// Writes the quads of a structure type whose first region lies in a rectangle of regions, both corners included,
    /// and returns their number. Only the lower 48 bits of the seed matter, and biomes are not looked at.
    /// </summary>
    int FindQuads(StructureType type, McVersion version, StructureRules rules, long seed, int regionX0, int regionZ0, int regionX1, int regionZ1, Span<QuadPosition> destination);
}

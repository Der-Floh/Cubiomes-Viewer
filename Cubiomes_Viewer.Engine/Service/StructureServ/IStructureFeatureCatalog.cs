using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

/// <summary>The ways the generator can tell the structures of a type apart, by which the map can hide some of them.</summary>
public interface IStructureFeatureCatalog
{
    /// <summary>The features of a structure type in a version, or none when the generator cannot tell its structures apart.</summary>
    IReadOnlyList<StructureFeature> GetFeatures(StructureType type, McVersion version);

    /// <summary>
    /// The bits of the options a described structure has, one per feature of its type; a feature the details say nothing
    /// about contributes no bit.
    /// </summary>
    ulong GetOptions(StructureDetails details, McVersion version);
}

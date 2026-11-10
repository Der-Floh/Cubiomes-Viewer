using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public interface IStructureCatalog
{
    /// <summary>The structure types the map can show, in display order.</summary>
    IReadOnlyList<StructureInfo> Structures { get; }

    StructureInfo GetInfo(StructureType type);

    /// <summary>Lists the structures that generate in one dimension of a version, in display order.</summary>
    IReadOnlyList<StructureInfo> GetAvailable(McVersion version, Dimension dimension);
}

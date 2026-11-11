using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Service.StructureDescriptionServ;

public interface IStructureDescriptionService
{
    /// <summary>Puts what the generator knows about a structure into short lines of text.</summary>
    IReadOnlyList<string> Describe(StructureDetails details, McVersion version);
}

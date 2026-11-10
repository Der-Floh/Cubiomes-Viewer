using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.WorldGenServ;

public interface IVersionCatalog
{
    /// <summary>All supported versions, oldest first.</summary>
    IReadOnlyList<McVersion> Versions { get; }
    McVersion Newest { get; }

    string GetName(McVersion version);
}

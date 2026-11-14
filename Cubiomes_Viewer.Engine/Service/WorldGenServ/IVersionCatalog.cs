using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.WorldGenServ;

public interface IVersionCatalog
{
    /// <summary>All supported versions, oldest first.</summary>
    IReadOnlyList<McVersion> Versions { get; }
    McVersion Newest { get; }

    string GetName(McVersion version);

    /// <summary>Finds a version by the name <see cref="GetName"/> gives it, in any letter case.</summary>
    bool TryGetVersion(string name, out McVersion version);

    /// <summary>
    /// Whether the generator supports a version less completely: the oldest ones and the Winter Drop, which this
    /// revision of cubiomes knew only before its release.
    /// </summary>
    bool IsExperimental(McVersion version);
}

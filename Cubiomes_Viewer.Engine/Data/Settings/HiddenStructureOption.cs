using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Settings;

/// <summary>An option of a structure feature that is unticked: structures that have it are not marked on the map.</summary>
public sealed class HiddenStructureOption
{
    public StructureType Type { get; set; }

    /// <summary>The key of the feature.</summary>
    public string Feature { get; set; } = string.Empty;

    /// <summary>The key of the option.</summary>
    public string Option { get; set; } = string.Empty;
}

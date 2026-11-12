using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Settings;

/// <summary>A salt entered for a structure type; it replaces the game's only while <see cref="IsReplacing"/> is set, so it can be switched off and on again.</summary>
public sealed class StructureSaltSetting
{
    public StructureType Type { get; set; }
    public int Salt { get; set; }
    public bool IsReplacing { get; set; }
}

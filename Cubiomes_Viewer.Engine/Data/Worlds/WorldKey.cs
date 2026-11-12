namespace Cubiomes_Viewer.Engine.Data.Worlds;

public readonly record struct WorldKey(long Seed, McVersion Version, Dimension Dimension, GeneratorFlags Flags)
{
    /// <summary>How structures generate in the world; the game's own rules unless they are changed.</summary>
    public StructureRules Rules
    {
        get => field ?? StructureRules.Default;
        init => field = value is null || value.IsDefault ? null : value;
    }
}

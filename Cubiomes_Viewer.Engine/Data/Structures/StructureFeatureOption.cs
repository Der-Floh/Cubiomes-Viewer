namespace Cubiomes_Viewer.Engine.Data.Structures;

/// <summary>One option of a <see cref="StructureFeature"/>.</summary>
public sealed record StructureFeatureOption
{
    /// <summary>Names the option in stored settings, so it never changes.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    /// <summary>The bit that stands for the option among the options a structure has; unique among the options of all features of a type.</summary>
    public required ulong Bit { get; init; }
}

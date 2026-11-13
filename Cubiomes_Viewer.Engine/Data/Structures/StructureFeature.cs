namespace Cubiomes_Viewer.Engine.Data.Structures;

/// <summary>
/// One way the structures of a type differ, such as the type of a bastion or whether a fortress has a nether wart
/// room. Every structure of the type has exactly one of its options, so structures can be hidden by the option they have.
/// </summary>
public sealed record StructureFeature
{
    /// <summary>Names the feature in stored settings, so it never changes.</summary>
    public required string Key { get; init; }

    public required string Name { get; init; }

    public required IReadOnlyList<StructureFeatureOption> Options { get; init; }
}

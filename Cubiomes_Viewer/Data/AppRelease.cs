namespace Cubiomes_Viewer.Data;

/// <summary>A released version of the app that the running one can update to.</summary>
public sealed record AppRelease
{
    public required string Version { get; init; }
}

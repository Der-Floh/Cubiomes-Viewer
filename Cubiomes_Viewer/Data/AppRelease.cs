namespace Cubiomes_Viewer.Data;

/// <summary>A released version of the app that is newer than the running one.</summary>
public sealed record AppRelease
{
    public required string Version { get; init; }

    /// <summary>The page of the release, which offers its downloads.</summary>
    public required Uri PageUrl { get; init; }
}

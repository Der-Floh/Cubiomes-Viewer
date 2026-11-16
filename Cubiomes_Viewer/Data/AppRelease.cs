namespace Cubiomes_Viewer.Data;

/// <summary>A released version of the app that the running one can update to.</summary>
public sealed record AppRelease
{
    public required string Version { get; init; }

    /// <summary>How many bytes the download of the release has, when the releases say so.</summary>
    public long? DownloadSize { get; init; }
}

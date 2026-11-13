namespace Cubiomes_Viewer.Cli.Data;

/// <summary>Runs the search of a session file, or continues it.</summary>
public sealed record SearchRequest
{
    public required string SessionPath { get; init; }

    /// <summary>The file the found seeds go to, or <see langword="null"/> for the standard output.</summary>
    public string? OutputPath { get; init; }

    /// <summary>Whether the search starts over instead of continuing where it stopped.</summary>
    public bool Reset { get; init; }
}

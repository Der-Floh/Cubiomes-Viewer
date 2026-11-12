namespace Cubiomes_Viewer.Cli.Data;

/// <summary>What the command line asks the CLI to do.</summary>
public abstract record CliRequest;

/// <summary>Runs the search of a session file, or continues it.</summary>
public sealed record SearchRequest : CliRequest
{
    public required string SessionPath { get; init; }

    /// <summary>The file the found seeds go to, or <see langword="null"/> for the standard output.</summary>
    public string? OutputPath { get; init; }

    /// <summary>Whether the search starts over instead of continuing where it stopped.</summary>
    public bool Reset { get; init; }
}

/// <summary>Checks that the generator on this machine produces the reference results.</summary>
public sealed record SelfTestRequest : CliRequest;

public sealed record HelpRequest : CliRequest;

public sealed record VersionRequest : CliRequest;

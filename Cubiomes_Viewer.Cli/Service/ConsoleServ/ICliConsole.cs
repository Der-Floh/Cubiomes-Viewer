namespace Cubiomes_Viewer.Cli.Service.ConsoleServ;

/// <summary>
/// The standard streams of the CLI. Seeds and reports go to the output; messages and the status of a running search go
/// to the error stream, so that the output can be piped or redirected on its own.
/// </summary>
public interface ICliConsole
{
    TextWriter Output { get; }

    /// <summary>Whether a status replaces the one before in the same line, as in a terminal, instead of adding a line.</summary>
    bool CanReplaceStatus { get; }

    /// <summary>Writes a line to the error stream; a status shown in place makes room for it.</summary>
    void WriteMessage(string message);

    /// <summary>Shows how a running search is doing.</summary>
    void WriteStatus(string status);

    /// <summary>Removes the status shown in place, if there is one.</summary>
    void ClearStatus();
}

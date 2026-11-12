namespace Cubiomes_Viewer.Cli.Repository.FileRepo;

/// <summary>The files named on the command line.</summary>
public interface IFileStore
{
    Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default);

    /// <summary>Replaces the content of a file in one step, so that it never holds part of the old and part of the new content.</summary>
    void Replace(string path, byte[] content);

    /// <summary>Creates a text file to write lines to, or empties the one that is there.</summary>
    TextWriter CreateText(string path);
}

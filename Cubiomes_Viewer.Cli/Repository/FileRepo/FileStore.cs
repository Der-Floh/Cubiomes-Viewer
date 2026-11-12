using System.Text;

namespace Cubiomes_Viewer.Cli.Repository.FileRepo;

public sealed class FileStore : IFileStore
{
    private const string TemporaryExtension = ".tmp";

    private static readonly UTF8Encoding _utf8 = new(encoderShouldEmitUTF8Identifier: false);

    public Task<byte[]> ReadAsync(string path, CancellationToken cancellationToken = default) => File.ReadAllBytesAsync(path, cancellationToken);

    public void Replace(string path, byte[] content)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        ArgumentNullException.ThrowIfNull(content);

        var temporaryPath = path + TemporaryExtension;
        try
        {
            File.WriteAllBytes(temporaryPath, content);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            DeleteLeftover(temporaryPath);
        }
    }

    public TextWriter CreateText(string path) => new StreamWriter(path, append: false, _utf8);

    private static void DeleteLeftover(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            // The next save writes over a leftover.
        }
    }
}

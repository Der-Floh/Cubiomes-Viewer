namespace Cubiomes_Viewer.Engine.Repository.AppDataRepo;

/// <summary>Stored content that cannot be read, for example because it is damaged or a newer version wrote it.</summary>
public sealed class UnreadableDataException : Exception
{
    /// <summary>The storage key the content was read from.</summary>
    public string Key { get; }

    /// <summary>The content as it was stored.</summary>
    public ReadOnlyMemory<byte> Content { get; }

    public UnreadableDataException(string key, ReadOnlyMemory<byte> content, string message, Exception? innerException = null)
        : base(message, innerException)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        Key = key;
        Content = content;
    }
}

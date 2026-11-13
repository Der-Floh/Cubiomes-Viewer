using Cubiomes_Viewer.Engine.Repository.AppDataRepo;

namespace Cubiomes_Viewer.Engine.Tests.Repository;

public sealed class FileSystemAppDataStoreTests : IDisposable
{
    private const string Key = "session.json";
    private const int Writes = 16;
    private const int ContentLength = 256 * 1024;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cubiomes-viewer-tests", Guid.NewGuid().ToString("N"));
    private readonly FileSystemAppDataStore _store;

    public FileSystemAppDataStoreTests()
    {
        _store = new FileSystemAppDataStore(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task WriteAsync_ManyWritesOfOneKeyAtOnce_LeaveOneOfThemWholeAndNoOtherFile()
    {
        var writes = Enumerable.Range(1, Writes).Select(value => Task.Run(() => _store.WriteAsync(Key, Enumerable.Repeat((byte)value, ContentLength).ToArray(), TestContext.Current.CancellationToken)));

        await Task.WhenAll(writes);

        var stored = await _store.ReadAsync(Key, TestContext.Current.CancellationToken);
        Assert.NotNull(stored);
        Assert.Equal(ContentLength, stored.Length);
        Assert.All(stored, value => Assert.Equal(stored[0], value));
        Assert.Equal([Key], Directory.GetFiles(_directory).Select(Path.GetFileName));
    }
}

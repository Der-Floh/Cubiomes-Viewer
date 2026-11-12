using Cubiomes_Viewer.Cli.Repository.FileRepo;

namespace Cubiomes_Viewer.Cli.Tests.Repository.FileRepo;

public sealed class FileStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cubiomes-viewer-tests", Guid.NewGuid().ToString("N"));
    private readonly FileStore _files = new();

    public FileStoreTests()
    {
        Directory.CreateDirectory(_directory);
    }

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task Replace_ExistingFile_HoldsOnlyTheNewContentAndLeavesNothingBehind()
    {
        var path = Path.Combine(_directory, "search.json");
        await File.WriteAllTextAsync(path, "old content that is longer", TestContext.Current.CancellationToken);

        _files.Replace(path, "new"u8.ToArray());

        Assert.Equal("new", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal(path, Assert.Single(Directory.GetFiles(_directory)));
    }

    [Fact]
    public async Task CreateText_ExistingFile_StartsItEmpty()
    {
        var path = Path.Combine(_directory, "seeds.txt");
        await File.WriteAllTextAsync(path, "1\n2\n", TestContext.Current.CancellationToken);

        await using (var writer = _files.CreateText(path))
            await writer.WriteAsync("3");

        Assert.Equal("3", await File.ReadAllTextAsync(path, TestContext.Current.CancellationToken));
    }
}

using Cubiomes_Viewer.Service.StorageServ;
using Cubiomes_Viewer.Service.TopLevelServ;

using Microsoft.Extensions.Logging.Abstractions;

namespace Cubiomes_Viewer.Tests.Service;

public sealed class StorageServiceTests : IDisposable
{
    private static readonly byte[] _oldContent = [1, 2, 3];

    private readonly string _directory = Directory.CreateTempSubdirectory("cubiomes-viewer-tests").FullName;
    private readonly StorageService _storage = new(new TopLevelProvider(), NullLogger<StorageService>.Instance);

    public void Dispose() => Directory.Delete(_directory, recursive: true);

    [Fact]
    public async Task WriteReplacing_Completed_ReplacesTheFile()
    {
        var path = await CreateFileAsync();

        await _storage.WriteReplacingAsync(path, stream => stream.WriteAsync(new byte[] { 9, 8 }).AsTask());

        Assert.Equal([9, 8], await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal([path], Directory.GetFiles(_directory));
    }

    [Fact]
    public async Task WriteReplacing_Stopped_KeepsTheFileThatWasThereAndLeavesNoOther()
    {
        var path = await CreateFileAsync();

        await Assert.ThrowsAsync<OperationCanceledException>(() => _storage.WriteReplacingAsync(path, async stream =>
        {
            await stream.WriteAsync(new byte[] { 9 });
            throw new OperationCanceledException();
        }));

        Assert.Equal(_oldContent, await File.ReadAllBytesAsync(path, TestContext.Current.CancellationToken));
        Assert.Equal([path], Directory.GetFiles(_directory));
    }

    private async Task<string> CreateFileAsync()
    {
        var path = Path.Combine(_directory, "map.png");
        await File.WriteAllBytesAsync(path, _oldContent, TestContext.Current.CancellationToken);
        return path;
    }
}

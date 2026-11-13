using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Service.AppDataServ;

using Microsoft.Extensions.Logging.Abstractions;

namespace Cubiomes_Viewer.Engine.Tests.Service.AppDataServ;

public sealed class UnreadableDataServiceTests : IDisposable
{
    private static readonly DateTimeOffset _now = new(2026, 11, 13, 15, 30, 5, TimeSpan.Zero);

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cubiomes-viewer-tests", Guid.NewGuid().ToString("N"));
    private readonly FileSystemAppDataStore _store;
    private readonly UnreadableDataService _service;

    public UnreadableDataServiceTests()
    {
        _store = new FileSystemAppDataStore(_directory);
        _service = new UnreadableDataService(_store, new FixedTimeProvider(_now), NullLogger<UnreadableDataService>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task KeepAsync_StoresTheContentUnderADatedKeyOfItsOwn()
    {
        byte[] content = [1, 2, 3];

        await _service.KeepAsync(new UnreadableDataException("settings.json", content, "Broken."), TestContext.Current.CancellationToken);

        Assert.Equal(["settings.unreadable-20261113-153005.json"], _service.CopyKeys);
        Assert.Equal(content, await _store.ReadAsync("settings.unreadable-20261113-153005.json", TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task KeepAsync_SameContentTwice_KeepsOneCopy()
    {
        byte[] content = [4, 5];

        await _service.KeepAsync(new UnreadableDataException("settings.json", content, "Broken."), TestContext.Current.CancellationToken);
        await _service.KeepAsync(new UnreadableDataException("settings.json", content, "Broken."), TestContext.Current.CancellationToken);

        Assert.Single(_service.CopyKeys);
    }

    [Fact]
    public async Task KeepAsync_ContentOfTwoKeys_KeepsACopyOfEach()
    {
        await _service.KeepAsync(new UnreadableDataException("settings.json", new byte[] { 1 }, "Broken."), TestContext.Current.CancellationToken);
        await _service.KeepAsync(new UnreadableDataException("color-maps.json", new byte[] { 1 }, "Broken."), TestContext.Current.CancellationToken);

        Assert.Equal(["settings.unreadable-20261113-153005.json", "color-maps.unreadable-20261113-153005.json"], _service.CopyKeys);
    }

    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;

        public override DateTimeOffset GetUtcNow() => now;
    }
}

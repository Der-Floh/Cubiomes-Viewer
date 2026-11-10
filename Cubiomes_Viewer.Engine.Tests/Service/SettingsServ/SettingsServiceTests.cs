using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Repository.SettingsRepo;
using Cubiomes_Viewer.Engine.Service.SettingsServ;

using Microsoft.Extensions.Logging.Abstractions;

namespace Cubiomes_Viewer.Engine.Tests.Service.SettingsServ;

public sealed class SettingsServiceTests
{
    [Fact]
    public async Task LoadAsync_StoredSettings_BecomeCurrent()
    {
        var stored = new AppSettings();
        var service = new SettingsService(new FakeRepository { Stored = stored }, NullLogger<SettingsService>.Instance);

        await service.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Same(stored, service.Current);
    }

    [Fact]
    public async Task LoadAsync_StorageCannotBeRead_KeepsTheDefaultsAndNeverOverwritesTheStorage()
    {
        var repository = new FakeRepository();
        var service = new SettingsService(repository, NullLogger<SettingsService>.Instance);
        var defaults = service.Current;

        await service.LoadAsync(TestContext.Current.CancellationToken);
        service.RequestSave();
        await service.FlushAsync(TestContext.Current.CancellationToken);

        Assert.Same(defaults, service.Current);
        Assert.Null(repository.Saved);
    }

    [Fact]
    public async Task FlushAsync_BeforeLoading_SavesNothing()
    {
        var repository = new FakeRepository { Stored = new AppSettings() };
        var service = new SettingsService(repository, NullLogger<SettingsService>.Instance);

        service.RequestSave();
        await service.FlushAsync(TestContext.Current.CancellationToken);

        Assert.Null(repository.Saved);
    }

    [Fact]
    public async Task FlushAsync_AfterChanges_WritesTheCurrentSettings()
    {
        var repository = new FakeRepository { Stored = new AppSettings() };
        var service = new SettingsService(repository, NullLogger<SettingsService>.Instance);
        await service.LoadAsync(TestContext.Current.CancellationToken);

        service.RequestSave();
        await service.FlushAsync(TestContext.Current.CancellationToken);

        Assert.Same(service.Current, repository.Saved);
    }

    private sealed class FakeRepository : ISettingsRepository
    {
        public AppSettings? Stored { get; init; }
        public AppSettings? Saved { get; private set; }

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
            => Stored is null
                ? Task.FromException<AppSettings>(new IOException("The storage is not available."))
                : Task.FromResult(Stored);

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Saved = settings;
            return Task.CompletedTask;
        }
    }
}

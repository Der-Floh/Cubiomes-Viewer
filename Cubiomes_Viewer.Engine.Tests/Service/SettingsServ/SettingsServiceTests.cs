using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Repository.SettingsRepo;
using Cubiomes_Viewer.Engine.Service.AppDataServ;
using Cubiomes_Viewer.Engine.Service.SettingsServ;

using Microsoft.Extensions.Logging.Abstractions;

namespace Cubiomes_Viewer.Engine.Tests.Service.SettingsServ;

public sealed class SettingsServiceTests
{
    private readonly FakeUnreadableData _unreadable = new();

    [Fact]
    public async Task LoadAsync_StoredSettings_BecomeCurrent()
    {
        var stored = new AppSettings();
        var service = CreateService(new FakeRepository { Stored = stored });

        await service.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Same(stored, service.Current);
    }

    [Fact]
    public async Task LoadAsync_StorageCannotBeRead_KeepsTheDefaultsAndNeverOverwritesTheStorage()
    {
        var repository = new FakeRepository();
        var service = CreateService(repository);
        var defaults = service.Current;

        await service.LoadAsync(TestContext.Current.CancellationToken);
        service.RequestSave();
        await service.FlushAsync(TestContext.Current.CancellationToken);

        Assert.Same(defaults, service.Current);
        Assert.Null(repository.Saved);
    }

    [Fact]
    public async Task LoadAsync_UnreadableSettings_KeepsACopyAndStoresTheDefaultsFromThenOn()
    {
        var unreadable = new UnreadableDataException(SettingsRepository.StorageKey, new byte[] { 1, 2, 3 }, "The settings are broken.");
        var repository = new FakeRepository { LoadFailure = unreadable };
        var service = CreateService(repository);

        await service.LoadAsync(TestContext.Current.CancellationToken);
        await service.FlushAsync(TestContext.Current.CancellationToken);

        Assert.Same(unreadable, Assert.Single(_unreadable.Kept));
        Assert.Same(service.Current, repository.Saved);
    }

    [Fact]
    public async Task LoadAsync_UnreadableSettingsThatCannotBeCopied_NeverOverwritesTheStorage()
    {
        var repository = new FakeRepository { LoadFailure = new UnreadableDataException(SettingsRepository.StorageKey, new byte[] { 1 }, "The settings are broken.") };
        _unreadable.Failure = new IOException("The disk is full.");
        var service = CreateService(repository);

        await service.LoadAsync(TestContext.Current.CancellationToken);
        await service.FlushAsync(TestContext.Current.CancellationToken);

        Assert.Null(repository.Saved);
    }

    [Fact]
    public async Task FlushAsync_BeforeLoading_SavesNothing()
    {
        var repository = new FakeRepository { Stored = new AppSettings() };
        var service = CreateService(repository);

        service.RequestSave();
        await service.FlushAsync(TestContext.Current.CancellationToken);

        Assert.Null(repository.Saved);
    }

    [Fact]
    public async Task FlushAsync_AfterChanges_WritesTheCurrentSettings()
    {
        var repository = new FakeRepository { Stored = new AppSettings() };
        var service = CreateService(repository);
        await service.LoadAsync(TestContext.Current.CancellationToken);

        service.RequestSave();
        await service.FlushAsync(TestContext.Current.CancellationToken);

        Assert.Same(service.Current, repository.Saved);
    }

    [Fact]
    public async Task FlushAsync_WhileADelayedSaveIsWriting_WritesAfterIt()
    {
        var repository = new FakeRepository { Stored = new AppSettings(), HoldsFirstSave = true };
        var service = CreateService(repository);
        await service.LoadAsync(TestContext.Current.CancellationToken);

        service.RequestSave();
        await repository.FirstSaveStarted.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        var flush = service.FlushAsync(TestContext.Current.CancellationToken);
        await Task.Delay(50, TestContext.Current.CancellationToken);
        var savesBeforeTheFirstEnded = repository.SaveCount;
        repository.ReleaseFirstSave();
        await flush;

        Assert.Equal(1, savesBeforeTheFirstEnded);
        Assert.Equal(2, repository.SaveCount);
    }

    private SettingsService CreateService(FakeRepository repository) => new(repository, _unreadable, NullLogger<SettingsService>.Instance);

    private sealed class FakeRepository : ISettingsRepository
    {
        private readonly TaskCompletionSource _firstSaveRelease = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _saveCount;

        public AppSettings? Stored { get; init; }
        public Exception? LoadFailure { get; init; }
        public bool HoldsFirstSave { get; init; }
        public AppSettings? Saved { get; private set; }
        public int SaveCount => Volatile.Read(ref _saveCount);
        public TaskCompletionSource FirstSaveStarted { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
        {
            if (LoadFailure is not null)
                return Task.FromException<AppSettings>(LoadFailure);
            return Stored is null
                ? Task.FromException<AppSettings>(new IOException("The storage is not available."))
                : Task.FromResult(Stored);
        }

        public Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            Saved = settings;
            if (Interlocked.Increment(ref _saveCount) > 1 || !HoldsFirstSave)
                return Task.CompletedTask;

            FirstSaveStarted.SetResult();
            return _firstSaveRelease.Task;
        }

        public void ReleaseFirstSave() => _firstSaveRelease.SetResult();
    }

    private sealed class FakeUnreadableData : IUnreadableDataService
    {
        public List<UnreadableDataException> Kept { get; } = [];
        public Exception? Failure { get; set; }
        public IReadOnlyList<string> CopyKeys => [.. Kept.Select(unreadable => unreadable.Key)];

        public Task KeepAsync(UnreadableDataException unreadable, CancellationToken cancellationToken = default)
        {
            if (Failure is not null)
                return Task.FromException(Failure);

            Kept.Add(unreadable);
            return Task.CompletedTask;
        }
    }
}

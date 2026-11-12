using System.Text;

using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Repository.SettingsRepo;

using Microsoft.Extensions.Logging.Abstractions;

namespace Cubiomes_Viewer.Engine.Tests.Repository;

public sealed class SettingsRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cubiomes-viewer-tests", Guid.NewGuid().ToString("N"));
    private readonly FileSystemAppDataStore _store;
    private readonly SettingsRepository _repository;

    public SettingsRepositoryTests()
    {
        _store = new FileSystemAppDataStore(_directory);
        _repository = new SettingsRepository(_store, NullLogger<SettingsRepository>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task LoadAsync_NothingStored_ReturnsDefaults()
    {
        var settings = await _repository.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AppTheme.System, settings.Appearance.Theme);
        Assert.Equal(MapSettings.DefaultCacheMegabytes, settings.Map.CacheMegabytes);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsEverySection()
    {
        var settings = new AppSettings
        {
            Appearance = { Theme = AppTheme.Dark },
            Map = { CacheMegabytes = 512, SmoothMotion = false, ShowGrid = false },
            World = { ShowExperimentalVersions = true, EstimateTerrain = false, Salts = [new StructureSaltSetting { Type = StructureType.Bastion, Salt = -7, IsReplacing = true }] },
            View =
            {
                Seed = -42, Version = McVersion.V1_16_5, LargeBiomes = true, Dimension = Dimension.Nether,
                SampleHeight = 32, CenterX = 12.5, CenterZ = -99, BlocksPerPixel = 0.5, RecentSeeds = [3, 2, 1],
            },
        };

        await _repository.SaveAsync(settings, TestContext.Current.CancellationToken);
        var loaded = await _repository.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AppTheme.Dark, loaded.Appearance.Theme);
        Assert.Equal(512, loaded.Map.CacheMegabytes);
        Assert.False(loaded.Map.SmoothMotion);
        Assert.False(loaded.Map.ShowGrid);
        Assert.True(loaded.World.ShowExperimentalVersions);
        Assert.Equal(new StructureRules { EstimateTerrain = false, Salts = [new StructureSalt(StructureType.Bastion, -7)] }, loaded.World.ToRules());
        Assert.Equal(-42, loaded.View.Seed);
        Assert.Equal(McVersion.V1_16_5, loaded.View.Version);
        Assert.True(loaded.View.LargeBiomes);
        Assert.Equal(Dimension.Nether, loaded.View.Dimension);
        Assert.Equal(32, loaded.View.SampleHeight);
        Assert.Equal(12.5, loaded.View.CenterX);
        Assert.Equal(-99, loaded.View.CenterZ);
        Assert.Equal(0.5, loaded.View.BlocksPerPixel);
        Assert.Equal([3, 2, 1], loaded.View.RecentSeeds);
    }

    [Fact]
    public async Task LoadAsync_CorruptContent_FallsBackToDefaults()
    {
        await _store.WriteAsync(SettingsRepository.StorageKey, Encoding.UTF8.GetBytes("{ not json"), TestContext.Current.CancellationToken);

        var settings = await _repository.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AppSettings.CurrentSchemaVersion, settings.SchemaVersion);
    }

    [Fact]
    public async Task DeleteAsync_StoredKey_RemovesTheContent()
    {
        await _store.WriteAsync("note.txt", new byte[] { 1, 2, 3 }, TestContext.Current.CancellationToken);

        await _store.DeleteAsync("note.txt", TestContext.Current.CancellationToken);

        Assert.Null(await _store.ReadAsync("note.txt", TestContext.Current.CancellationToken));
    }

    [Theory]
    [InlineData("../escape.json")]
    [InlineData("sub/file.json")]
    [InlineData(" ")]
    public async Task WriteAsync_KeyWithDirectoryOrBlank_ThrowsArgumentException(string key)
        => await Assert.ThrowsAnyAsync<ArgumentException>(() => _store.WriteAsync(key, new byte[] { 1 }, TestContext.Current.CancellationToken));
}

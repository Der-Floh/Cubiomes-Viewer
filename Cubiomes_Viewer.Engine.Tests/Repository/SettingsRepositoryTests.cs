using System.Text;

using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Repository.SettingsRepo;

namespace Cubiomes_Viewer.Engine.Tests.Repository;

public sealed class SettingsRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cubiomes-viewer-tests", Guid.NewGuid().ToString("N"));
    private readonly FileSystemAppDataStore _store;
    private readonly SettingsRepository _repository;

    public SettingsRepositoryTests()
    {
        _store = new FileSystemAppDataStore(_directory);
        _repository = new SettingsRepository(_store);
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
            Map =
            {
                CacheMegabytes = 512, SmoothMotion = false, ShowGrid = false, Pins = [new MapPin { Seed = -42, Dimension = Dimension.Nether, X = 7, Z = -9, Name = "Blaze farm", Icon = "Fortress" }],
                Measurements = [new MapMeasurement { Seed = -42, Dimension = Dimension.Nether, StartX = 1, StartZ = 2, EndX = -30, EndZ = 44, Name = "Tunnel" }],
            },
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
        var pin = Assert.Single(loaded.Map.Pins);
        Assert.Equal((-42L, Dimension.Nether, 7, -9, "Blaze farm", "Fortress"), (pin.Seed, pin.Dimension, pin.X, pin.Z, pin.Name, pin.Icon));
        var measurement = Assert.Single(loaded.Map.Measurements);
        Assert.Equal((-42L, Dimension.Nether, new BlockPos(1, 2), new BlockPos(-30, 44), "Tunnel"), (measurement.Seed, measurement.Dimension, measurement.Start, measurement.End, measurement.Name));
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

    [Theory]
    [InlineData("{ not json")]
    [InlineData("""{ "appearance": { "theme": "Sepia" } }""")]
    [InlineData("null")]
    public async Task LoadAsync_ContentItCannotRead_ThrowsWithTheContent(string stored)
    {
        var content = Encoding.UTF8.GetBytes(stored);
        await _store.WriteAsync(SettingsRepository.StorageKey, content, TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<UnreadableDataException>(() => _repository.LoadAsync(TestContext.Current.CancellationToken));

        Assert.Equal(SettingsRepository.StorageKey, exception.Key);
        Assert.Equal(content, exception.Content.ToArray());
    }

    [Fact]
    public async Task LoadAsync_SettingNoLongerKept_IsSkipped()
    {
        var content = Encoding.UTF8.GetBytes("""{ "appearance": { "theme": "Dark", "markerStyle": "Clean" } }""");
        await _store.WriteAsync(SettingsRepository.StorageKey, content, TestContext.Current.CancellationToken);

        var settings = await _repository.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(AppTheme.Dark, settings.Appearance.Theme);
    }

    [Fact]
    public async Task LoadAsync_SettingsOfANewerVersion_Throws()
    {
        var content = Encoding.UTF8.GetBytes($$"""{ "schemaVersion": {{AppSettings.CurrentSchemaVersion + 1}} }""");
        await _store.WriteAsync(SettingsRepository.StorageKey, content, TestContext.Current.CancellationToken);

        var exception = await Assert.ThrowsAsync<UnreadableDataException>(() => _repository.LoadAsync(TestContext.Current.CancellationToken));

        Assert.Contains("newer version", exception.Message, StringComparison.Ordinal);
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

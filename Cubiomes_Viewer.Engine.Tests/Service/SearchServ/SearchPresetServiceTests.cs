using System.Text;

using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Repository.PresetRepo;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Engine.Tests.Data.Search;

using Microsoft.Extensions.Logging.Abstractions;

namespace Cubiomes_Viewer.Engine.Tests.Service.SearchServ;

public sealed class SearchPresetServiceTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cubiomes-viewer-tests", Guid.NewGuid().ToString("N"));
    private readonly FileSystemAppDataStore _store;
    private readonly SearchSetup _setup = new() { Definition = SampleDefinitions.CreateEveryKindOfCondition(), Source = new SeedListSource { Seeds = [1, 2] }, MaxResults = 9 };

    public SearchPresetServiceTests()
    {
        _store = new FileSystemAppDataStore(_directory);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task SaveAsync_ThenLoadAsyncElsewhere_KeepsThePresetsSortedByName()
    {
        var service = CreateService();
        await service.LoadAsync(TestContext.Current.CancellationToken);

        await service.SaveAsync("  zebra  ", _setup, TestContext.Current.CancellationToken);
        await service.SaveAsync("Apple", _setup with { MaxResults = 1 }, TestContext.Current.CancellationToken);
        var reloaded = CreateService();
        await reloaded.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(["Apple", "zebra"], reloaded.UserPresets.Select(preset => preset.Name));
        Assert.Equal(_setup, reloaded.UserPresets[1].Setup);
    }

    [Fact]
    public async Task SaveAsync_NameThatExists_ReplacesThatPresetWhateverItsCase()
    {
        var service = CreateService();
        await service.LoadAsync(TestContext.Current.CancellationToken);
        await service.SaveAsync("Quads", _setup, TestContext.Current.CancellationToken);

        await service.SaveAsync("QUADS", _setup with { MaxResults = 3 }, TestContext.Current.CancellationToken);

        var preset = Assert.Single(service.UserPresets);
        Assert.Equal(("QUADS", 3), (preset.Name, preset.Setup.MaxResults));
    }

    [Fact]
    public async Task DeleteAsync_Preset_RemovesItAndTellsTheListeners()
    {
        var service = CreateService();
        await service.LoadAsync(TestContext.Current.CancellationToken);
        await service.SaveAsync("Quads", _setup, TestContext.Current.CancellationToken);
        var changes = 0;
        service.UserPresetsChanged += (_, _) => changes++;

        await service.DeleteAsync("quads", TestContext.Current.CancellationToken);

        Assert.Empty(service.UserPresets);
        Assert.Equal(1, changes);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task SaveAsync_BlankName_Throws(string name)
    {
        var service = CreateService();

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => service.SaveAsync(name, _setup, TestContext.Current.CancellationToken));

        Assert.StartsWith("Give the preset a name.", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveAsync_NameTooLong_Throws()
        => await Assert.ThrowsAsync<ArgumentException>(() => CreateService().SaveAsync(new string('x', SearchPresetService.MaxNameLength + 1), _setup, TestContext.Current.CancellationToken));

    [Fact]
    public async Task LoadAsync_UnreadableStore_StartsWithoutPresets()
    {
        await _store.WriteAsync(SearchPresetRepository.StorageKey, Encoding.UTF8.GetBytes("{ not json"), TestContext.Current.CancellationToken);
        var service = CreateService();

        await service.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Empty(service.UserPresets);
        Assert.NotEmpty(service.Examples);
    }

    private SearchPresetService CreateService() => new(new SearchPresetRepository(_store, NullLogger<SearchPresetRepository>.Instance));
}

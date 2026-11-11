using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Repository.SettingsRepo;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class LayersPanelViewModelTests : IDisposable
{
    private readonly ServiceProvider _services = TestServices.Create();
    private readonly MainViewModel _main;
    private readonly LayersPanelViewModel _layers;

    public LayersPanelViewModelTests()
    {
        _main = _services.GetRequiredService<MainViewModel>();
        _layers = _services.GetRequiredService<LayersPanelViewModel>();
    }

    public void Dispose() => _services.Dispose();

    [AvaloniaFact]
    public async Task Initialization_DefaultSettings_ListsOverworldStructuresWithTheCommonOnesShown()
    {
        await _main.Initialization;

        Assert.True(_layers.IsOverworld);
        Assert.True(_layers.HasStructures);
        Assert.True(GetStructure("Village").IsChecked);
        Assert.False(GetStructure("Mineshaft").IsChecked);
        Assert.DoesNotContain(_layers.Structures, structure => structure.Name == "Nether fortress");
        Assert.Contains(StructureType.Village, _main.Map.VisibleStructures);
        Assert.DoesNotContain(StructureType.Mineshaft, _main.Map.VisibleStructures);
        Assert.True(_main.Map.ShowSpawn);
        Assert.True(_main.Map.ShowStrongholds);
        Assert.False(_main.Map.ShowSlimeChunks);
        Assert.True(_main.Map.ShowGatewayLinks);
        Assert.False(_layers.IsEnd);
    }

    [AvaloniaFact]
    public async Task Toggles_Changed_UpdateTheMapAndTheStoredSettings()
    {
        await _main.Initialization;

        GetStructure("Mineshaft").IsChecked = true;
        _layers.Spawn.IsChecked = false;
        _layers.ShowSlimeChunks = true;
        await _services.GetRequiredService<ISettingsService>().FlushAsync(TestContext.Current.CancellationToken);

        var stored = await _services.GetRequiredService<ISettingsRepository>().LoadAsync(TestContext.Current.CancellationToken);
        Assert.Contains(StructureType.Mineshaft, _main.Map.VisibleStructures);
        Assert.False(_main.Map.ShowSpawn);
        Assert.True(_main.Map.ShowSlimeChunks);
        Assert.Contains(StructureType.Mineshaft, stored.Map.VisibleStructures);
        Assert.False(stored.Map.ShowSpawn);
        Assert.True(stored.Map.ShowStrongholds);
        Assert.True(stored.Map.ShowSlimeChunks);
    }

    [AvaloniaFact]
    public async Task Dimension_ChangedToNether_ListsNetherStructuresAndHidesOverworldOverlays()
    {
        await _main.Initialization;

        _services.GetRequiredService<WorldSession>().Dimension = Dimension.Nether;

        Assert.False(_layers.IsOverworld);
        Assert.Equal(["Nether fortress", "Bastion remnant", "Ruined portal"], _layers.Structures.Select(structure => structure.Name));
        Assert.Equal([true, true, false], _layers.Structures.Select(structure => structure.IsChecked));
    }

    [AvaloniaFact]
    public async Task GatewayLinks_SwitchedOffInTheEnd_HideOnTheMapAndAreStored()
    {
        await _main.Initialization;
        _services.GetRequiredService<WorldSession>().Dimension = Dimension.End;

        _layers.ShowGatewayLinks = false;
        await _services.GetRequiredService<ISettingsService>().FlushAsync(TestContext.Current.CancellationToken);

        var stored = await _services.GetRequiredService<ISettingsRepository>().LoadAsync(TestContext.Current.CancellationToken);
        Assert.True(_layers.IsEnd);
        Assert.False(_layers.IsOverworld);
        Assert.False(_main.Map.ShowGatewayLinks);
        Assert.False(stored.Map.ShowGatewayLinks);
    }

    [AvaloniaFact]
    public async Task Version_WithoutStructures_ShowsAnEmptyList()
    {
        await _main.Initialization;

        _services.GetRequiredService<WorldSession>().Version = McVersion.Beta1_7;

        Assert.False(_layers.HasStructures);
        Assert.Empty(_layers.Structures);
    }

    [AvaloniaFact]
    public async Task HideAllThenShowAll_Structures_SwitchOnlyTheListedTypes()
    {
        await _main.Initialization;

        _layers.HideAllStructuresCommand.Execute(null);
        Assert.All(_layers.Structures, structure => Assert.False(structure.IsChecked));
        Assert.DoesNotContain(StructureType.Village, _main.Map.VisibleStructures);
        Assert.Contains(StructureType.Fortress, _main.Map.VisibleStructures);

        _layers.ShowAllStructuresCommand.Execute(null);
        Assert.All(_layers.Structures, structure => Assert.True(structure.IsChecked));
        Assert.Contains(StructureType.Geode, _main.Map.VisibleStructures);
    }

    private MarkerToggleViewModel GetStructure(string name) => _layers.Structures.Single(structure => structure.Name == name);
}

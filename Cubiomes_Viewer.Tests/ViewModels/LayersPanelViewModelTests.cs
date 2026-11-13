using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Repository.SettingsRepo;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
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
        Assert.True(GetStructure("Village").Toggle.IsChecked);
        Assert.False(GetStructure("Mineshaft").Toggle.IsChecked);
        Assert.DoesNotContain(_layers.Structures, structure => structure.Toggle.Name == "Nether fortress");
        Assert.Contains(StructureType.Village, _main.Map.VisibleStructures);
        Assert.DoesNotContain(StructureType.Mineshaft, _main.Map.VisibleStructures);
        Assert.True(_main.Map.ShowSpawn);
        Assert.True(_main.Map.ShowStrongholds);
        Assert.False(_main.Map.ShowSlimeChunks);
        Assert.True(_main.Map.ShowGatewayLinks);
        Assert.False(_layers.IsEnd);
    }

    [AvaloniaFact]
    public async Task Initialization_DefaultSettings_OffersTheLayersOfTheWorldAndShowsBiomes()
    {
        await _main.Initialization;

        Assert.Equal(
            ["Biomes", "Temperature", "Humidity", "Continentalness", "Erosion", "Depth", "Weirdness", "Surface height", "Structure potential"],
            _layers.Layers.Select(layer => layer.Display));
        Assert.Equal(LayerMode.Biomes, _layers.SelectedLayer?.Value);
        Assert.Equal(LayerMode.Biomes, _main.Map.Layer);
        Assert.False(_layers.HasOctaves);
    }

    [AvaloniaFact]
    public async Task SelectedLayer_ClimateLayerWithAnOctaveLimit_IsShownOnTheMapAndStored()
    {
        await _main.Initialization;

        _layers.SelectedLayer = _layers.Layers.Single(layer => layer.Value == LayerMode.Temperature);
        Assert.True(_layers.HasOctaves);
        Assert.Equal(["All octaves", "1 octave", "2 octaves", "3 octaves"], _layers.OctaveChoices.Select(choice => choice.Display));

        _layers.SelectedOctaves = _layers.OctaveChoices.Single(choice => choice.Value == 2);
        await _services.GetRequiredService<ISettingsService>().FlushAsync(TestContext.Current.CancellationToken);

        var stored = await _services.GetRequiredService<ISettingsRepository>().LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(LayerMode.Temperature, _main.Map.Layer);
        Assert.Equal(2, _main.Map.Octaves);
        Assert.Equal(LayerMode.Temperature, stored.View.Layer);
        Assert.Equal(2, stored.View.Octaves);
    }

    [AvaloniaFact]
    public async Task SelectedLayer_ChangedToALayerWithFewerOctaves_ShowsAllOctavesAgain()
    {
        await _main.Initialization;
        var session = _services.GetRequiredService<WorldSession>();
        _layers.SelectedLayer = _layers.Layers.Single(layer => layer.Value == LayerMode.Continentalness);
        _layers.SelectedOctaves = _layers.OctaveChoices.Single(choice => choice.Value == 10);

        _layers.SelectedLayer = _layers.Layers.Single(layer => layer.Value == LayerMode.Temperature);

        Assert.Equal(0, session.Octaves);
        Assert.Equal(0, _layers.SelectedOctaves?.Value);
    }

    [AvaloniaFact]
    public async Task Version_ChangedToOneWithoutTheShownLayer_FallsBackToBiomes()
    {
        await _main.Initialization;
        var session = _services.GetRequiredService<WorldSession>();
        _layers.SelectedLayer = _layers.Layers.Single(layer => layer.Value == LayerMode.Erosion);

        session.Version = McVersion.V1_16_5;

        Assert.Equal(["Biomes", "Rivers", "Ocean temperature", "Surface height", "Structure potential"], _layers.Layers.Select(layer => layer.Display));
        Assert.Equal(LayerMode.Biomes, _layers.SelectedLayer?.Value);
        Assert.Equal(LayerMode.Biomes, session.Layer);
        Assert.Equal(LayerMode.Biomes, _main.Map.Layer);
        Assert.False(_layers.HasOctaves);
    }

    [AvaloniaFact]
    public async Task Dimension_ChangedWhileHeightIsShown_KeepsTheLayerWhereTheDimensionHasIt()
    {
        await _main.Initialization;
        var session = _services.GetRequiredService<WorldSession>();
        _layers.SelectedLayer = _layers.Layers.Single(layer => layer.Value == LayerMode.Height);

        session.Dimension = Dimension.End;
        Assert.Equal(LayerMode.Height, _main.Map.Layer);

        session.Dimension = Dimension.Nether;
        Assert.Equal(LayerMode.Biomes, _main.Map.Layer);
        Assert.Equal(["Biomes", "Structure potential"], _layers.Layers.Select(layer => layer.Display));
    }

    [AvaloniaFact]
    public async Task SelectedHeightStyle_Changed_IsOfferedForTheHeightLayerOnlyAndStored()
    {
        await _main.Initialization;
        Assert.False(_layers.IsHeightShown);
        Assert.Equal(HeightStyle.Shaded, _layers.SelectedHeightStyle?.Value);

        _layers.SelectedLayer = _layers.Layers.Single(layer => layer.Value == LayerMode.Height);
        _layers.SelectedHeightStyle = _layers.HeightStyles.Single(style => style.Value == HeightStyle.Contours);
        await _services.GetRequiredService<ISettingsService>().FlushAsync(TestContext.Current.CancellationToken);

        var stored = await _services.GetRequiredService<ISettingsRepository>().LoadAsync(TestContext.Current.CancellationToken);
        Assert.True(_layers.IsHeightShown);
        Assert.Equal(HeightStyle.Contours, _main.Map.HeightStyle);
        Assert.Equal(HeightStyle.Contours, stored.Map.HeightStyle);
        Assert.Equal(["Shaded relief", "Height colours", "Contour lines", "Grayscale"], _layers.HeightStyles.Select(style => style.Display));
    }

    [AvaloniaFact]
    public async Task SelectedPotentialStructure_Changed_IsOfferedForThePotentialLayerOnlyAndStored()
    {
        await _main.Initialization;
        Assert.False(_layers.IsPotentialShown);
        Assert.Equal(StructureType.Village, _layers.SelectedPotentialStructure?.Value);
        Assert.DoesNotContain(_layers.PotentialStructures, structure => structure.Value == StructureType.Geode);

        _layers.SelectedLayer = _layers.Layers.Single(layer => layer.Value == LayerMode.StructurePotential);
        _layers.SelectedPotentialStructure = _layers.PotentialStructures.Single(structure => structure.Value == StructureType.Mansion);
        await _services.GetRequiredService<ISettingsService>().FlushAsync(TestContext.Current.CancellationToken);

        var stored = await _services.GetRequiredService<ISettingsRepository>().LoadAsync(TestContext.Current.CancellationToken);
        Assert.True(_layers.IsPotentialShown);
        Assert.Equal(LayerMode.StructurePotential, _main.Map.Layer);
        Assert.Equal(StructureType.Mansion, _main.Map.PotentialStructure);
        Assert.Equal(StructureType.Mansion, stored.Map.PotentialStructure);
    }

    [AvaloniaFact]
    public async Task Dimension_ChangedWhileThePotentialIsShown_OffersTheStructuresOfThatDimension()
    {
        await _main.Initialization;
        _layers.SelectedLayer = _layers.Layers.Single(layer => layer.Value == LayerMode.StructurePotential);

        _services.GetRequiredService<WorldSession>().Dimension = Dimension.Nether;

        Assert.Equal(LayerMode.StructurePotential, _main.Map.Layer);
        Assert.Equal(["Nether fortress", "Bastion remnant", "Ruined portal"], _layers.PotentialStructures.Select(structure => structure.Display));
        Assert.Equal(StructureType.Fortress, _layers.SelectedPotentialStructure?.Value);
        Assert.Equal(StructureType.Fortress, _main.Map.PotentialStructure);
    }

    [AvaloniaFact]
    public async Task Toggles_Changed_UpdateTheMapAndTheStoredSettings()
    {
        await _main.Initialization;

        GetStructure("Mineshaft").Toggle.IsChecked = true;
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
        Assert.Equal(["Nether fortress", "Bastion remnant", "Ruined portal"], _layers.Structures.Select(structure => structure.Toggle.Name));
        Assert.Equal([true, true, false], _layers.Structures.Select(structure => structure.Toggle.IsChecked));
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
        Assert.Equal(["Biomes", "Temperature", "Humidity", "Biomes without oceans"], _layers.Layers.Select(layer => layer.Display));
    }

    [AvaloniaFact]
    public async Task HideAllThenShowAll_Structures_SwitchOnlyTheListedTypes()
    {
        await _main.Initialization;

        _layers.HideAllStructuresCommand.Execute(null);
        Assert.All(_layers.Structures, structure => Assert.False(structure.Toggle.IsChecked));
        Assert.DoesNotContain(StructureType.Village, _main.Map.VisibleStructures);
        Assert.Contains(StructureType.Fortress, _main.Map.VisibleStructures);

        _layers.ShowAllStructuresCommand.Execute(null);
        Assert.All(_layers.Structures, structure => Assert.True(structure.Toggle.IsChecked));
        Assert.Contains(StructureType.Geode, _main.Map.VisibleStructures);
    }

    [AvaloniaFact]
    public async Task Features_InTheNether_AreOfferedForEachStructureWithEveryOptionTicked()
    {
        await _main.Initialization;
        Assert.False(GetStructure("Desert pyramid").HasFeatures);

        _services.GetRequiredService<WorldSession>().Dimension = Dimension.Nether;

        var fortress = GetStructure("Nether fortress");
        Assert.Equal(["Nether wart room", "Blaze spawner"], fortress.Features.Select(feature => feature.Name));
        Assert.All(fortress.Features, feature => Assert.Equal(["With", "Without"], feature.Options.Select(option => option.Name)));
        Assert.Equal(["Housing units", "Hoglin stables", "Treasure room", "Bridge"], GetStructure("Bastion remnant").Features.Single().Options.Select(option => option.Name));
        Assert.All(_layers.Structures, structure => Assert.True(structure.HasFeatures));
        Assert.All(_layers.Structures.SelectMany(structure => structure.Features).SelectMany(feature => feature.Options), option => Assert.True(option.IsChecked));
        Assert.DoesNotContain(_layers.Structures, structure => structure.IsFiltered);
        Assert.Empty(_main.Map.HiddenStructureOptions);
    }

    [AvaloniaFact]
    public async Task FeatureOption_Unticked_HidesTheStructuresWithItOnTheMapAndIsStored()
    {
        await _main.Initialization;
        _services.GetRequiredService<WorldSession>().Dimension = Dimension.Nether;
        var fortress = GetStructure("Nether fortress");

        GetOption(fortress, "Nether wart room", "Without").IsChecked = false;
        await _services.GetRequiredService<ISettingsService>().FlushAsync(TestContext.Current.CancellationToken);

        var stored = await _services.GetRequiredService<ISettingsRepository>().LoadAsync(TestContext.Current.CancellationToken);
        var hidden = Assert.Single(stored.Map.HiddenStructureOptions);
        var shownOnMap = Assert.Single(_main.Map.HiddenStructureOptions);
        Assert.True(fortress.IsFiltered);
        Assert.Equal(StructureType.Fortress, shownOnMap.Key);
        Assert.Equal(GetBit(StructureType.Fortress, McVersion.V1_21_3, "netherWartRoom", "without"), shownOnMap.Value);
        Assert.Equal((StructureType.Fortress, "netherWartRoom", "without"), (hidden.Type, hidden.Feature, hidden.Option));
    }

    [AvaloniaFact]
    public async Task ShowAllOptions_AfterSomeWereUnticked_MarksEveryStructureAgain()
    {
        await _main.Initialization;
        _services.GetRequiredService<WorldSession>().Dimension = Dimension.Nether;
        var bastion = GetStructure("Bastion remnant");
        GetOption(bastion, "Type", "Housing units").IsChecked = false;
        GetOption(bastion, "Type", "Bridge").IsChecked = false;
        Assert.True(bastion.IsFiltered);

        bastion.ShowAllOptionsCommand.Execute(null);
        await _services.GetRequiredService<ISettingsService>().FlushAsync(TestContext.Current.CancellationToken);

        var stored = await _services.GetRequiredService<ISettingsRepository>().LoadAsync(TestContext.Current.CancellationToken);
        Assert.False(bastion.IsFiltered);
        Assert.All(bastion.Features.Single().Options, option => Assert.True(option.IsChecked));
        Assert.Empty(_main.Map.HiddenStructureOptions);
        Assert.Empty(stored.Map.HiddenStructureOptions);
    }

    [AvaloniaFact]
    public async Task Version_WithoutAFeatureWhoseOptionIsUnticked_LeavesItOutUntilTheVersionHasItAgain()
    {
        await _main.Initialization;
        var session = _services.GetRequiredService<WorldSession>();
        GetOption(GetStructure("Village"), "Type", "Desert").IsChecked = false;

        session.Version = McVersion.V1_12_2;
        Assert.Equal(["Villagers"], GetStructure("Village").Features.Select(feature => feature.Name));
        Assert.False(GetStructure("Village").IsFiltered);
        Assert.Empty(_main.Map.HiddenStructureOptions);

        session.Version = McVersion.V1_21_3;
        Assert.False(GetOption(GetStructure("Village"), "Type", "Desert").IsChecked);
        Assert.Equal(GetBit(StructureType.Village, McVersion.V1_21_3, "type", "desert"), _main.Map.HiddenStructureOptions[StructureType.Village]);
    }

    [AvaloniaFact]
    public async Task Initialization_StoredHiddenOption_IsShownUntickedAndHiddenOnTheMap()
    {
        using var services = TestServices.Create();
        var stored = new AppSettings
        {
            Map = { HiddenStructureOptions = [new HiddenStructureOption { Type = StructureType.Fortress, Feature = "blazeSpawner", Option = "without" }] },
            View = { Dimension = Dimension.Nether },
        };
        await services.GetRequiredService<ISettingsRepository>().SaveAsync(stored, TestContext.Current.CancellationToken);

        var main = services.GetRequiredService<MainViewModel>();
        await main.Initialization;

        var fortress = services.GetRequiredService<LayersPanelViewModel>().Structures.Single(structure => structure.Type == StructureType.Fortress);
        Assert.True(fortress.IsFiltered);
        Assert.False(GetOption(fortress, "Blaze spawner", "Without").IsChecked);
        Assert.True(GetOption(fortress, "Blaze spawner", "With").IsChecked);
        Assert.Equal(GetBit(StructureType.Fortress, McVersion.V1_21_3, "blazeSpawner", "without"), main.Map.HiddenStructureOptions[StructureType.Fortress]);
    }

    private StructureToggleViewModel GetStructure(string name) => _layers.Structures.Single(structure => structure.Toggle.Name == name);

    private static StructureFeatureOptionViewModel GetOption(StructureToggleViewModel structure, string feature, string option)
        => structure.Features.Single(candidate => candidate.Name == feature).Options.Single(candidate => candidate.Name == option);

    private ulong GetBit(StructureType type, McVersion version, string feature, string option)
        => _services.GetRequiredService<IStructureFeatureCatalog>().GetFeatures(type, version)
            .Single(candidate => candidate.Key == feature).Options.Single(candidate => candidate.Key == option).Bit;
}

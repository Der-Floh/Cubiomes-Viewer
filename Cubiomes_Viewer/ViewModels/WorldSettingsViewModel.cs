using System.Collections.ObjectModel;
using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>How worlds generate beyond seed and version: the versions offered, the terrain estimate and the structure salts.</summary>
public sealed partial class WorldSettingsViewModel : ObservableObject
{
    public string Title { get; } = "World generation";
    public string ExperimentalVersionsLabel { get; } = "Show experimental versions";
    public string ExperimentalVersionsHint { get; } = "Adds Beta 1.7, Beta 1.8, 1.0 and the 1.21 Winter Drop, which the generator supports less completely.";
    public string EstimateTerrainLabel { get; } = "Estimate the terrain";
    public string EstimateTerrainHint { get; } = "From 1.18 on, leaves out desert pyramids, jungle temples and woodland mansions where an estimate puts the land too low for them. The estimate can be wrong either way.";
    public string SaltsLabel { get; } = "Structure salts";
    public string SaltsHint { get; } = "Servers and data packs can place structures with other salts than the game. Tick a structure and enter its salt to see their worlds; searches and analysis use it too.";
    public string GameSaltsLabel { get; } = "Use the game's salts";

    [ObservableProperty]
    public partial bool ShowExperimentalVersions { get; set; }

    [ObservableProperty]
    public partial bool EstimateTerrain { get; set; } = true;

    [ObservableProperty]
    public partial bool HasReplacedSalts { get; set; }

    /// <summary>The structure types of the current version whose positions depend on a salt.</summary>
    public ObservableCollection<StructureSaltViewModel> Salts { get; } = [];

    private readonly WorldSession _session;
    private readonly WorldBarViewModel _worldBar;
    private readonly IStructureCatalog _structureCatalog;
    private readonly IStructureFinder _structureFinder;
    private readonly ISettingsService _settingsService;
    private bool _isShowingSettings;

    public WorldSettingsViewModel(WorldSession session, WorldBarViewModel worldBar, IStructureCatalog structureCatalog, IStructureFinder structureFinder, ISettingsService settingsService)
    {
        _session = session;
        _worldBar = worldBar;
        _structureCatalog = structureCatalog;
        _structureFinder = structureFinder;
        _settingsService = settingsService;

        ShowSalts();
        session.PropertyChanged += OnSessionChanged;
    }

    /// <summary>Shows and applies the stored settings; called once they are loaded.</summary>
    public void ShowSettings()
    {
        var settings = _settingsService.Current.World;
        Show(() =>
        {
            ShowExperimentalVersions = settings.ShowExperimentalVersions;
            EstimateTerrain = settings.EstimateTerrain;
            ShowSalts();
        });
        Apply();
    }

    [RelayCommand]
    public void UseGameSalts()
    {
        Show(() =>
        {
            foreach (var salt in Salts)
                salt.IsReplacing = false;
        });
        Store();
    }

    partial void OnShowExperimentalVersionsChanged(bool value) => Store();

    partial void OnEstimateTerrainChanged(bool value) => Store();

    private void OnSaltChanged(StructureSaltViewModel salt) => Store();

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(WorldSession.Version))
            Show(ShowSalts);
    }

    private void ShowSalts()
    {
        var stored = _settingsService.Current.World.Salts;
        var types = GetSaltedTypes(_session.Version);
        Salts.Clear();
        foreach (var (structure, gameSalt) in types)
        {
            var name = types.Count(other => other.Structure.Name == structure.Name) > 1 ? $"{structure.Name} ({DescribeDimension(structure.Type)})" : structure.Name;
            Salts.Add(new StructureSaltViewModel(structure.Type, name, gameSalt, stored.LastOrDefault(salt => salt.Type == structure.Type), OnSaltChanged));
        }

        HasReplacedSalts = Salts.Any(salt => salt.IsReplacing);
    }

    private List<(StructureInfo Structure, int GameSalt)> GetSaltedTypes(McVersion version)
    {
        List<(StructureInfo, int)> types = [];
        foreach (var structure in _structureCatalog.Structures)
        {
            if (_structureFinder.TryGetGameSalt(structure.Type, version, out var gameSalt))
                types.Add((structure, gameSalt));
        }

        return types;
    }

    private string DescribeDimension(StructureType type)
        => _structureFinder.TryGetPlacement(type, _session.Version, out var placement) ? placement.Dimension.ToString() : string.Empty;

    private void Store()
    {
        if (_isShowingSettings)
            return;

        var settings = _settingsService.Current.World;
        var shown = Salts.Select(salt => salt.Type).ToHashSet();
        settings.ShowExperimentalVersions = ShowExperimentalVersions;
        settings.EstimateTerrain = EstimateTerrain;
        settings.Salts = [.. settings.Salts.Where(salt => !shown.Contains(salt.Type)), .. Salts.Where(salt => salt.IsWorthStoring).Select(salt => salt.ToSetting())];
        _settingsService.RequestSave();

        HasReplacedSalts = Salts.Any(salt => salt.IsReplacing);
        Apply();
    }

    private void Apply()
    {
        var settings = _settingsService.Current.World;
        _session.Rules = settings.ToRules();
        _worldBar.IncludeExperimentalVersions = settings.ShowExperimentalVersions;
    }

    private void Show(Action changes)
    {
        var wasShowing = _isShowingSettings;
        _isShowingSettings = true;
        try
        {
            changes();
        }
        finally
        {
            _isShowingSettings = wasShowing;
        }
    }
}

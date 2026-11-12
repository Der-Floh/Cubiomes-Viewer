using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

using Avalonia.Platform.Storage;
using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ColorMapServ;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.StorageServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>
/// The colours biomes are painted in: the default ones or one of the user's colour maps, which are made, edited,
/// shared and removed here. Edits show on the map at once and are kept only when saved.
/// </summary>
public sealed partial class BiomeColorsViewModel : ViewModelBase
{
    private const string NewMapName = "My colours";

    private static readonly FilePickerFileType _colorMapFiles = new("Colour maps") { Patterns = ["*.json"], MimeTypes = ["application/json"] };

    public string Title { get; } = "Biome colours";
    public string NewLabel { get; } = "New";
    public string EditLabel { get; } = "Edit";
    public string ImportLabel { get; } = "Import…";
    public string ExportLabel { get; } = "Export…";
    public string DeleteLabel { get; } = "Delete";
    public string NameLabel { get; } = "Name";
    public string FilterPlaceholder { get; } = "Find a biome";
    public string EditHint { get; } = "Pick a colour to change it; the map shows it at once. The biomes of the version on the map are listed.";
    public string AllDefaultLabel { get; } = "All default";
    public string AllDimmedLabel { get; } = "All dimmed";
    public string SaveLabel { get; } = "Save";
    public string CancelLabel { get; } = "Cancel";
    public string SavedTitle { get; } = "Saved";
    public string ImportedTitle { get; } = "Imported";

    [ObservableProperty]
    public partial ComboChoice<string?>? SelectedMap { get; set; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(EditCommand), nameof(ExportCommand), nameof(DeleteCommand))]
    public partial bool HasUserMap { get; set; }

    [ObservableProperty]
    public partial bool IsEditing { get; set; }

    [ObservableProperty]
    public partial string EditName { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    /// <summary>The default colours and the stored maps.</summary>
    public ObservableCollection<ComboChoice<string?>> MapChoices { get; } = [];

    /// <summary>The biomes being edited that the filter lets through.</summary>
    public ObservableCollection<BiomeColorEntryViewModel> Entries { get; } = [];

    private readonly MapViewModel _map;
    private readonly WorldSession _session;
    private readonly IColorMapService _colorMaps;
    private readonly IBiomeCatalog _biomeCatalog;
    private readonly ISettingsService _settingsService;
    private readonly IStorageService _storage;
    private readonly INotificationService _notifications;
    private List<BiomeColorEntryViewModel> _allEntries = [];
    private IReadOnlyList<BiomeColor> _editedColors = [];
    private string? _editedName;
    private bool _isPreviewQueued;
    private bool _isShowing;

    public BiomeColorsViewModel(MapViewModel map, WorldSession session, IColorMapService colorMaps, IBiomeCatalog biomeCatalog, ISettingsService settingsService, IStorageService storage, INotificationService notifications) : base(notifications)
    {
        _map = map;
        _session = session;
        _colorMaps = colorMaps;
        _biomeCatalog = biomeCatalog;
        _settingsService = settingsService;
        _storage = storage;
        _notifications = notifications;

        ShowChoices(null);
        session.PropertyChanged += OnSessionChanged;
    }

    private BiomeColorMap? Selected => SelectedMap?.Value is { } name ? _colorMaps.Maps.FirstOrDefault(map => map.Name == name) : null;

    /// <summary>Loads the stored maps and paints the map with the one chosen last.</summary>
    public async Task LoadAsync()
    {
        await _colorMaps.LoadAsync();
        ShowChoices(_settingsService.Current.Map.ColorMap);
        ApplySelected();
    }

    [RelayCommand]
    public void New() => StartEditing(_colorMaps.GetFreeName(NewMapName), null, Selected?.Colors ?? []);

    [RelayCommand(CanExecute = nameof(HasUserMap))]
    public void Edit()
    {
        if (Selected is { } map)
            StartEditing(map.Name, map.Name, map.Colors);
    }

    [RelayCommand]
    public Task SaveAsync() => RunGuardedAsync(async () =>
    {
        var name = EditName.Trim();
        if (name.Length is 0 or > BiomeColorMap.MaxNameLength)
            throw new InvalidOperationException($"Give the colour map a name of at most {BiomeColorMap.MaxNameLength} characters.");
        if (!string.Equals(name, _editedName, StringComparison.OrdinalIgnoreCase) && _colorMaps.Maps.Any(map => string.Equals(map.Name, name, StringComparison.OrdinalIgnoreCase)))
            throw new InvalidOperationException($"There is a colour map called '{name}' already.");

        var listed = _allEntries.Select(entry => entry.Biome).ToHashSet();
        var map = new BiomeColorMap
        {
            Name = name,
            Colors = [.. _editedColors.Where(color => !listed.Contains(color.Biome)), .. _allEntries.Where(entry => entry.IsChanged).Select(entry => new BiomeColor(entry.Biome, entry.RgbColor))],
        };
        await _colorMaps.SaveAsync(map, _editedName);
        StopEditing();
        ShowChoices(map.Name);
        ApplySelected();
        _notifications.ShowSuccess(SavedTitle, map.Name);
    });

    [RelayCommand]
    public void Cancel()
    {
        StopEditing();
        ApplySelected();
    }

    [RelayCommand]
    public void UseAllDefault() => SetAll(entry => entry.DefaultColor);

    [RelayCommand]
    public void UseAllDimmed() => SetAll(entry => entry.DefaultColor.ToDimmed());

    [RelayCommand(CanExecute = nameof(HasUserMap))]
    public Task DeleteAsync() => RunGuardedAsync(async () =>
    {
        if (Selected is not { } map)
            return;

        await _colorMaps.DeleteAsync(map.Name);
        StopEditing();
        ShowChoices(null);
        ApplySelected();
    });

    [RelayCommand]
    public Task ImportAsync() => RunGuardedAsync(async () =>
    {
        var options = new FilePickerOpenOptions { Title = ImportLabel, FileTypeFilter = [_colorMapFiles] };
        if (await _storage.OpenFileAsync(options) is not { } content)
            return;

        var imported = await _colorMaps.ImportAsync(content);
        if (imported.Count == 0)
            return;

        StopEditing();
        ShowChoices(imported[0].Name);
        ApplySelected();
        _notifications.ShowSuccess(ImportedTitle, string.Join(", ", imported.Select(map => map.Name)));
    }, browserFireAndForget: true);

    [RelayCommand(CanExecute = nameof(HasUserMap))]
    public Task ExportAsync() => RunGuardedAsync(async () =>
    {
        if (Selected is not { } map)
            return;

        var options = new FilePickerSaveOptions { Title = ExportLabel, SuggestedFileName = $"{map.Name}.json", DefaultExtension = "json", FileTypeChoices = [_colorMapFiles] };
        if (await _storage.SaveFileAsync(options, _colorMaps.Export(map)) is { } name)
            _notifications.ShowSuccess(SavedTitle, name);
    }, browserFireAndForget: true);

    partial void OnSelectedMapChanged(ComboChoice<string?>? value)
    {
        if (_isShowing)
            return;

        StopEditing();
        ApplySelected();
        _settingsService.Current.Map.ColorMap = value?.Value;
        _settingsService.RequestSave();
    }

    partial void OnFilterChanged(string value) => ShowEntries();

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(WorldSession.Version) || !IsEditing)
            return;

        var colors = _allEntries.Select(entry => new BiomeColor(entry.Biome, entry.RgbColor)).ToList();
        _allEntries = CreateEntries(_colorMaps.DefaultPalette.With([.. _editedColors, .. colors]));
        ShowEntries();
    }

    private void StartEditing(string name, string? editedName, IReadOnlyList<BiomeColor> colors)
    {
        EditName = name;
        _editedName = editedName;
        _editedColors = colors;
        _allEntries = CreateEntries(_colorMaps.DefaultPalette.With(colors));
        Filter = string.Empty;
        ShowEntries();
        IsEditing = true;
        Preview();
    }

    private void StopEditing()
    {
        IsEditing = false;
        _allEntries = [];
        Entries.Clear();
    }

    private List<BiomeColorEntryViewModel> CreateEntries(BiomePalette colors)
    {
        var version = _session.Version;
        var defaults = _colorMaps.DefaultPalette;
        var biomes = Enum.GetValues<Dimension>().SelectMany(dimension => _biomeCatalog.GetBiomes(version, dimension)).Distinct().Order();
        return [.. biomes.Select(biome => new BiomeColorEntryViewModel(biome, _biomeCatalog.GetDisplayName(version, biome) ?? biome.ToString(CultureInfo.InvariantCulture), defaults[biome], colors[biome], QueuePreview))];
    }

    private void ShowEntries()
    {
        Entries.Clear();
        var filter = Filter?.Trim() ?? string.Empty;
        foreach (var entry in _allEntries.Where(entry => entry.Name.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            Entries.Add(entry);
    }

    private void SetAll(Func<BiomeColorEntryViewModel, RgbColor> color)
    {
        _isShowing = true;
        try
        {
            foreach (var entry in _allEntries)
                entry.SetColor(color(entry));
        }
        finally
        {
            _isShowing = false;
        }

        Preview();
    }

    /// <summary>Repaints the map once for many quick edits, such as dragging through a colour spectrum.</summary>
    private void QueuePreview()
    {
        if (_isShowing || !IsEditing || _isPreviewQueued)
            return;

        _isPreviewQueued = true;
        Dispatcher.UIThread.Post(() =>
        {
            _isPreviewQueued = false;
            if (IsEditing)
                Preview();
        });
    }

    private void Preview() => _map.BiomeColors = _colorMaps.DefaultPalette.With([.. _editedColors, .. _allEntries.Select(entry => new BiomeColor(entry.Biome, entry.RgbColor))]);

    private void ApplySelected()
    {
        var map = Selected;
        HasUserMap = map is not null;
        _map.BiomeColors = map is null ? null : _colorMaps.GetPalette(map);
    }

    /// <summary>Offers the default colours and the stored maps, choosing the wanted one where it is offered.</summary>
    private void ShowChoices(string? wanted)
    {
        _isShowing = true;
        try
        {
            MapChoices.Clear();
            MapChoices.Add(new ComboChoice<string?> { Display = "Default colours", Value = null });
            foreach (var map in _colorMaps.Maps)
                MapChoices.Add(new ComboChoice<string?> { Display = map.Name, Value = map.Name });
            SelectedMap = MapChoices.FirstOrDefault(choice => choice.Value == wanted) ?? MapChoices[0];
        }
        finally
        {
            _isShowing = false;
        }

        _settingsService.Current.Map.ColorMap = SelectedMap?.Value;
        _settingsService.RequestSave();
    }
}

using System.ComponentModel;

using Avalonia.Platform.Storage;

using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.StorageServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>The search panel: what to look for, the search itself and what it found, kept between runs of the app and in files.</summary>
public sealed partial class SearchPanelViewModel : ViewModelBase
{
    private const string SuggestedFileName = "search.json";
    private const string FileExtension = "json";
    private static readonly FilePickerFileType _sessionFileType = new("Cubiomes Viewer search")
    {
        Patterns = ["*.json"],
        MimeTypes = ["application/json"],
    };

    public string Title { get; } = "Search";
    public string SessionLabel { get; } = "Keep the search";
    public string SessionHint { get; } = "The search stays between runs of the app. A file takes it elsewhere.";
    public string SaveLabel { get; } = "Save to file";
    public string OpenLabel { get; } = "Open file";

    public SearchSetupViewModel Setup { get; }
    public SearchRunViewModel Run { get; }
    public SearchResultsViewModel Results { get; }
    public SearchPresetsViewModel Presets { get; }

    private readonly ISearchSessionService _sessions;
    private readonly IStorageService _storage;
    private readonly INotificationService _notifications;

    public SearchPanelViewModel(SearchSetupViewModel setup, SearchRunViewModel run, SearchResultsViewModel results, SearchPresetsViewModel presets, ISearchSessionService sessions, IStorageService storage, INotificationService notifications) : base(notifications)
    {
        Setup = setup;
        Run = run;
        Results = results;
        Presets = presets;
        _sessions = sessions;
        _storage = storage;
        _notifications = notifications;

        setup.Changed += OnSetupChanged;
        run.PropertyChanged += OnRunChanged;
        presets.Opened += OnPresetOpened;
    }

    /// <summary>Loads the stored session and the user's presets and shows them; called once the settings are loaded.</summary>
    /// <summary>Loads the presets, and the search of last time when <paramref name="restoreSession"/> is set.</summary>
    public async Task LoadAsync(bool restoreSession = true)
    {
        if (restoreSession)
        {
            await _sessions.LoadAsync();
            ShowSession();
        }

        await Presets.LoadAsync();
    }

    [RelayCommand]
    private Task SaveSessionAsync() => RunGuardedAsync(async () =>
    {
        var options = new FilePickerSaveOptions
        {
            Title = SaveLabel,
            SuggestedFileName = SuggestedFileName,
            DefaultExtension = FileExtension,
            FileTypeChoices = [_sessionFileType],
        };

        if (await _storage.SaveFileAsync(options, _sessions.Export()) is { } fileName)
            _notifications.ShowSuccess("Search saved", fileName);
    }, browserFireAndForget: true);

    [RelayCommand(CanExecute = nameof(CanOpenSession))]
    private Task OpenSessionAsync() => RunGuardedAsync(async () =>
    {
        var options = new FilePickerOpenOptions { Title = OpenLabel, FileTypeFilter = [_sessionFileType] };
        if (await _storage.OpenFileAsync(options) is not { } content)
            return;

        _sessions.Import(content);
        ShowSession();
    }, browserFireAndForget: true);

    private bool CanOpenSession() => !Run.IsRunning;

    private void ShowSession() => Setup.Show(_sessions.Setup);

    private void OnPresetOpened(object? sender, SearchPreset preset) => Setup.Show(preset.Setup);

    private void OnSetupChanged(object? sender, EventArgs e) => _sessions.Setup = Setup.Setup;

    private void OnRunChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchRunViewModel.IsRunning))
            OpenSessionCommand.NotifyCanExecuteChanged();
    }
}

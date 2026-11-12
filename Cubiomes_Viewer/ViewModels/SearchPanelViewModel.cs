using System.ComponentModel;

using Avalonia.Platform.Storage;

using CommunityToolkit.Mvvm.Input;

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

    private readonly ISearchSessionService _sessions;
    private readonly IStorageService _storage;
    private readonly INotificationService _notifications;

    public SearchPanelViewModel(SearchSetupViewModel setup, SearchRunViewModel run, SearchResultsViewModel results, ISearchSessionService sessions, IStorageService storage, INotificationService notifications) : base(notifications)
    {
        Setup = setup;
        Run = run;
        Results = results;
        _sessions = sessions;
        _storage = storage;
        _notifications = notifications;

        setup.Changed += OnSetupChanged;
        run.PropertyChanged += OnRunChanged;
    }

    /// <summary>Shows the stored setup; called once the session is loaded.</summary>
    public void ShowSession() => Setup.Show(_sessions.Setup);

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

    private void OnSetupChanged(object? sender, EventArgs e) => _sessions.Setup = Setup.Setup;

    private void OnRunChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SearchRunViewModel.IsRunning))
            OpenSessionCommand.NotifyCanExecuteChanged();
    }
}

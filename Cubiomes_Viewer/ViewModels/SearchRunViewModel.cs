using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Service.NotificationServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Starts, continues and stops the search set up in the panel and shows how far it has come.</summary>
public sealed partial class SearchRunViewModel : ViewModelBase
{
    public string StartLabel { get; } = "Search";
    public string ContinueLabel { get; } = "Continue";
    public string StopLabel { get; } = "Stop";
    public string ClearLabel { get; } = "Clear";
    public string ClearHint { get; } = "Forget the search and the seeds it found.";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(StartCommand), nameof(StopCommand), nameof(ClearCommand))]
    public partial bool IsRunning { get; set; }

    /// <summary>Whether a stopped search can continue, which it can only with the setup it started with.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ContinueCommand))]
    public partial bool CanContinue { get; set; }

    [ObservableProperty]
    public partial bool IsStartShown { get; set; } = true;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ClearCommand))]
    public partial bool HasSearch { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ProgressText { get; set; } = string.Empty;

    private readonly ISeedSearchService _search;
    private readonly SearchSetupViewModel _setup;
    private readonly INotificationService _notifications;
    private CancellationTokenSource? _stopping;

    public SearchRunViewModel(ISeedSearchService search, SearchSetupViewModel setup, INotificationService notifications) : base(notifications)
    {
        _search = search;
        _setup = setup;
        _notifications = notifications;

        ShowSearch();
        search.Changed += OnSearchChanged;
        setup.Changed += OnSetupChanged;
    }

    [RelayCommand(CanExecute = nameof(CanStart))]
    private Task StartAsync() => RunGuardedAsync(() => RunAsync(cancellationToken => _search.StartAsync(_setup.Setup, cancellationToken)));

    [RelayCommand(CanExecute = nameof(CanContinue))]
    private Task ContinueAsync() => RunGuardedAsync(() => RunAsync(_search.ResumeAsync));

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Stop() => _stopping?.Cancel();

    [RelayCommand(CanExecute = nameof(CanClear))]
    private void Clear() => _search.Reset();

    private bool CanStart() => !IsRunning && _setup.IsValid;

    private bool CanClear() => !IsRunning && HasSearch;

    private async Task RunAsync(Func<CancellationToken, Task> search)
    {
        using var stopping = new CancellationTokenSource();
        _stopping = stopping;
        try
        {
            await search(stopping.Token);
        }
        finally
        {
            _stopping = null;
        }

        if (_search.State == SearchState.Finished)
            _notifications.ShowSuccess("Search finished", $"Found {SearchProgressText.Count(_search.Results.Count, "seed")}.");
    }

    private void OnSearchChanged(object? sender, EventArgs e) => ShowSearch();

    private void OnSetupChanged(object? sender, EventArgs e)
    {
        ShowSearch();
        StartCommand.NotifyCanExecuteChanged();
    }

    private void ShowSearch()
    {
        var state = _search.State;
        IsRunning = state == SearchState.Running;
        CanContinue = state == SearchState.Paused && _search.Setup == _setup.Setup;
        IsStartShown = !IsRunning && !CanContinue;
        HasSearch = state != SearchState.Idle;
        StatusText = SearchProgressText.DescribeState(state, _search.Progress, _search.Results.Count, _search.Setup?.MaxResults ?? 0, CanContinue);
        ProgressText = SearchProgressText.DescribeProgress(state, _search.Progress);
    }
}

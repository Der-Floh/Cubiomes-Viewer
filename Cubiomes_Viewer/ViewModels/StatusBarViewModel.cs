using Avalonia.Threading;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Service.ProgressServ;
using Cubiomes_Viewer.Service.UpdateServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>
/// The strip under the map: what the pointer is on and the scale of the map, the version of the app, and how far the action
/// shown has come, filling the strip from the left while it runs.
/// </summary>
public sealed partial class StatusBarViewModel : ViewModelBase
{
    private readonly IProgressService _progress;

    public StatusBarViewModel(MapViewModel map, IUpdateService updates, IProgressService progress)
    {
        Map = map;
        VersionText = $"v{updates.CurrentVersion}";
        _progress = progress;
        _progress.Changed += OnProgressChanged;
        ShowProgress();
    }

    public MapViewModel Map { get; }

    public string VersionText { get; }

    [ObservableProperty]
    public partial double ProgressShare { get; set; }

    [ObservableProperty]
    public partial bool IsProgressShown { get; set; }

    private void OnProgressChanged(object? sender, EventArgs e)
    {
        if (Dispatcher.UIThread.CheckAccess())
            ShowProgress();
        else
            Dispatcher.UIThread.Post(ShowProgress);
    }

    /// <summary>Sets the share before the bar appears and clears it after the bar is gone, so it never shows a stale fill.</summary>
    private void ShowProgress()
    {
        if (_progress.Share is { } share)
        {
            ProgressShare = share;
            IsProgressShown = true;
        }
        else
        {
            IsProgressShown = false;
            ProgressShare = 0;
        }
    }
}

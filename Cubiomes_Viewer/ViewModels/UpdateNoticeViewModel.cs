using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Service.BrowserServ;
using Cubiomes_Viewer.Service.NotificationServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>
/// The notice at start that a newer release is out. It opens the page of the release, goes away, or turns these notices
/// off until Settings → Updates turns them on again.
/// </summary>
public sealed partial class UpdateNoticeViewModel : ViewModelBase
{
    public string Title { get; } = "Update available";
    public string Message { get; }
    public string OpenLabel { get; } = "Open release";
    public string DismissLabel { get; } = "Dismiss";
    public string DontShowAgainLabel { get; } = "Don't show again";

    private readonly AppRelease _release;
    private readonly IBrowserService _browser;
    private readonly Action _turnOffNotices;

    public UpdateNoticeViewModel(AppRelease release, string currentVersion, IBrowserService browser, Action turnOffNotices, INotificationService notifications) : base(notifications)
    {
        _release = release;
        _browser = browser;
        _turnOffNotices = turnOffNotices;
        Message = $"{Constants.AppName} {release.Version} is out; you have {currentVersion}.";
    }

    [RelayCommand]
    private Task OpenAsync() => RunGuardedAsync(() => _browser.OpenAsync(_release.PageUrl));

    [RelayCommand]
    private void DontShowAgain() => _turnOffNotices();
}

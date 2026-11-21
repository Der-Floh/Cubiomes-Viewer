using Avalonia.Controls;
using Avalonia.Controls.Notifications;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.ViewModels;

namespace Cubiomes_Viewer.Tests.Views;

/// <summary>The notice of a newer release as the notification area shows it, clicked the way a user does.</summary>
public sealed class UpdateNoticeViewTests : IDisposable
{
    private static readonly AppRelease _release = new() { Version = "5.2.0", PageUrl = new Uri("https://github.com/Der-Floh/Cubiomes-Viewer/releases/tag/v5.2.0") };

    private readonly RecordingBrowserService _browser = new();
    private readonly WindowNotificationManager _manager = new();
    private readonly Window _window;
    private bool _isTurnedOff;

    public UpdateNoticeViewTests()
    {
        _window = new Window { Width = 800, Height = 600, Content = new Panel { Children = { _manager } } };
        _window.Show();
        var notice = new UpdateNoticeViewModel(_release, "5.1.0", _browser, () => _isTurnedOff = true, new RecordingNotificationService());
        _manager.Show(notice, NotificationType.Information, TimeSpan.Zero);
        WaitUntilShown();
    }

    public void Dispose() => _window.Close();

    [AvaloniaFact]
    public void Notice_Shown_TellsTheNewAndTheRunningVersion()
        => Assert.Contains(_window.GetVisualDescendants().OfType<TextBlock>(), text => text.Text == "Cubiomes Viewer 5.2.0 is out; you have 5.1.0.");

    [AvaloniaFact]
    public void Open_Clicked_OpensThePageOfTheReleaseAndCloses()
    {
        var card = FindCard();

        HeadlessInput.Click(FindButton("Open release"));

        Assert.Equal([_release.PageUrl], _browser.Pages);
        Assert.False(_isTurnedOff);
        Assert.True(card.IsClosing || card.IsClosed, "The notice stayed open.");
    }

    [AvaloniaFact]
    public void Dismiss_Clicked_ClosesAndDoesNothingElse()
    {
        var card = FindCard();

        HeadlessInput.Click(FindButton("Dismiss"));

        Assert.Empty(_browser.Pages);
        Assert.False(_isTurnedOff);
        Assert.True(card.IsClosing || card.IsClosed, "The notice stayed open.");
    }

    [AvaloniaFact]
    public void DontShowAgain_Clicked_TurnsTheNoticesOffAndCloses()
    {
        var card = FindCard();

        HeadlessInput.Click(FindButton("Don't show again"));

        Assert.True(_isTurnedOff);
        Assert.Empty(_browser.Pages);
        Assert.True(card.IsClosing || card.IsClosed, "The notice stayed open.");
    }

    /// <summary>Lets the card finish sliding in, since a click while it moves can miss its button on a slow machine.</summary>
    private void WaitUntilShown()
    {
        HeadlessInput.Render();
        var card = FindCard();
        Assert.True(SpinWait.SpinUntil(() =>
        {
            HeadlessInput.Render();
            return card.Opacity >= 1;
        }, TimeSpan.FromSeconds(5)), "The notice did not finish appearing.");
    }

    private NotificationCard FindCard() => _window.GetVisualDescendants().OfType<NotificationCard>().Single();

    private Button FindButton(string label) => _window.GetVisualDescendants().OfType<Button>().Single(button => Equals(button.Content, label));
}

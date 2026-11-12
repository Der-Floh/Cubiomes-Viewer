using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

namespace Cubiomes_Viewer.Tests;

/// <summary>Clicks controls the way a user does, in whichever window or popup they are shown.</summary>
internal static class HeadlessInput
{
    /// <summary>Presses and releases the left mouse button over the middle of a control, then lets the app react.</summary>
    public static void Click(Control control)
    {
        var root = TopLevel.GetTopLevel(control);
        Assert.NotNull(root);
        var center = control.TranslatePoint(new Point(control.Bounds.Width / 2, control.Bounds.Height / 2), root);
        Assert.NotNull(center);

        root.MouseDown(center.Value, MouseButton.Left);
        root.MouseUp(center.Value, MouseButton.Left);
        Render();
    }

    /// <summary>
    /// Opens the menu of a drop-down button and returns its items. The headless platform shows popups in the overlay
    /// layer of their window, so the items are looked up there by the entries they show.
    /// </summary>
    public static IReadOnlyList<MenuItem> OpenMenu(DropDownButton button)
    {
        Click(button);
        var menu = Assert.IsType<MenuFlyout>(button.Flyout);
        Assert.True(menu.IsOpen, "The menu did not open.");

        var entries = menu.ItemsSource?.Cast<object>().ToHashSet() ?? [];
        var root = TopLevel.GetTopLevel(button);
        Assert.NotNull(root);
        return [.. root.GetVisualDescendants().OfType<MenuItem>().Where(item => item.DataContext is { } entry && entries.Contains(entry))];
    }

    public static void Render()
    {
        Dispatcher.UIThread.RunJobs();
        AvaloniaHeadlessPlatform.ForceRenderTimerTick();
        Dispatcher.UIThread.RunJobs();
    }
}

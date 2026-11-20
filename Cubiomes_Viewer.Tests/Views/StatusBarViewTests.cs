using Avalonia;
using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.VisualTree;

using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Service.ProgressServ;
using Cubiomes_Viewer.Service.UpdateServ;
using Cubiomes_Viewer.Tests.Controls;
using Cubiomes_Viewer.ViewModels;
using Cubiomes_Viewer.Views;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.Views;

/// <summary>The strip under the map as it is drawn: the bar that fills it while an action runs, and the texts over it.</summary>
public sealed class StatusBarViewTests : IDisposable
{
    private const int StripWidth = 800;
    private const int StripHeight = 40;
    private const int TopBorder = 1;
    private const int TextInset = 12;
    private const int TextGap = 16;

    private readonly ServiceProvider _services = TestServices.Create(services => services.AddSingleton<IUpdateService>(new FakeUpdateService { CurrentVersion = "1.2.3" }));
    private readonly StatusBarViewModel _statusBar;
    private readonly IProgressService _progress;
    private readonly Window _window;

    public StatusBarViewTests()
    {
        _statusBar = _services.GetRequiredService<StatusBarViewModel>();
        _progress = _services.GetRequiredService<IProgressService>();
        _window = new Window { Width = StripWidth, Height = StripHeight, Content = new StatusBarView { DataContext = _statusBar } };
        _window.Show();
        HeadlessInput.Render();
    }

    public void Dispose()
    {
        _window.Close();
        _services.Dispose();
    }

    [AvaloniaFact]
    public void Progress_Halfway_FillsTheLeftHalfOfTheStripUnderItsBorder()
    {
        using var action = _progress.Begin();

        action.Report(0.5);
        using var frame = CaptureFrame();

        Assert.Equal(new PixelRect(0, TopBorder, StripWidth / 2, StripHeight - TopBorder), FramePixels.Find(frame, GetColor("ProgressColor")).Bounds);
        Assert.Equal(StripWidth / 2, FramePixels.Find(frame, GetColor("RegionColor")).Bounds.X);
    }

    [AvaloniaFact]
    public void Progress_OfAnActionThatEnded_LeavesNoFill()
    {
        var action = _progress.Begin();
        action.Report(0.5);
        CaptureFrame().Dispose();

        action.Dispose();
        using var frame = CaptureFrame();

        Assert.Equal(0, FramePixels.Find(frame, GetColor("ProgressColor")).Count);
    }

    [AvaloniaFact]
    public void Version_IsTheLastTextOnTheRightAfterTheScale()
    {
        var version = GetBounds(_statusBar.VersionText);
        var scale = GetBounds(_statusBar.Map.ScaleText);

        Assert.Equal(StripWidth - TextInset, version.Right, tolerance: 0.5);
        Assert.Equal(version.Left - TextGap, scale.Right, tolerance: 0.5);
    }

    private WriteableBitmap CaptureFrame()
    {
        HeadlessInput.Render();
        return _window.CaptureRenderedFrame() ?? throw new InvalidOperationException("The window drew no frame.");
    }

    private RgbColor GetColor(string key)
        => _window.TryFindResource(key, _window.ActualThemeVariant, out var brush) && brush is ISolidColorBrush { Color: var color }
            ? new RgbColor(color.R, color.G, color.B)
            : throw new InvalidOperationException($"The theme has no brush {key}.");

    private Rect GetBounds(string text)
    {
        var block = _window.GetVisualDescendants().OfType<TextBlock>().Single(candidate => candidate.IsEffectivelyVisible && candidate.Text == text);
        var origin = block.TranslatePoint(default, _window) ?? throw new InvalidOperationException($"The text {text} is not in the window.");
        return new Rect(origin, block.Bounds.Size);
    }
}

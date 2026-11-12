using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Repository.SettingsRepo;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class MainViewModelTests : IDisposable
{
    private readonly ServiceProvider _services = TestServices.Create();

    public void Dispose() => _services.Dispose();

    [AvaloniaFact]
    public async Task Initialization_StoredView_IsRestoredIntoSessionMapAndPanels()
    {
        var stored = new AppSettings
        {
            Map = { ShowGrid = false, SmoothMotion = false },
            View =
            {
                Seed = 262, Version = McVersion.V1_18_2, Dimension = Dimension.End, LargeBiomes = true, SampleHeight = 70,
                CenterX = 100, CenterZ = -50, BlocksPerPixel = 2, RecentSeeds = [262, 1],
            },
        };
        await _services.GetRequiredService<ISettingsRepository>().SaveAsync(stored, TestContext.Current.CancellationToken);

        var main = _services.GetRequiredService<MainViewModel>();
        await main.Initialization;

        var session = _services.GetRequiredService<WorldSession>();
        Assert.Empty(Notifications.Errors);
        Assert.Equal(262, session.Seed);
        Assert.Equal(McVersion.V1_18_2, session.Version);
        Assert.Equal(Dimension.End, session.Dimension);
        Assert.True(session.LargeBiomes);
        Assert.Equal(70, session.SampleHeight);
        Assert.Equal(new MapCamera(100, -50, 2), main.Map.Camera);
        Assert.Equal(session.Key, main.Map.World);
        Assert.False(main.Map.ShowGrid);
        Assert.False(main.Map.SmoothMotion);
        Assert.Equal([262L, 1L], main.WorldBar.RecentSeeds.Select(recent => recent.Seed));
        Assert.Equal("262", main.WorldBar.SeedText);
    }

    [AvaloniaFact]
    public async Task ViewChanges_AfterInitialization_ArePersisted()
    {
        var main = _services.GetRequiredService<MainViewModel>();
        await main.Initialization;

        _services.GetRequiredService<WorldSession>().Seed = 77;
        main.Map.Camera = new MapCamera(5, 6, 8);
        await _services.GetRequiredService<ISettingsService>().FlushAsync(TestContext.Current.CancellationToken);

        var stored = await _services.GetRequiredService<ISettingsRepository>().LoadAsync(TestContext.Current.CancellationToken);
        Assert.Equal(77, stored.View.Seed);
        Assert.Equal(5, stored.View.CenterX);
        Assert.Equal(6, stored.View.CenterZ);
        Assert.Equal(8, stored.View.BlocksPerPixel);
    }

    [AvaloniaFact]
    public async Task SelectedPanelIndex_Changed_OpensAndClosesTheMatchingPanel()
    {
        var main = _services.GetRequiredService<MainViewModel>();
        await main.Initialization;
        Assert.False(main.IsPanelOpen);

        main.SelectedPanelIndex = 0;
        Assert.IsType<LayersPanelViewModel>(main.CurrentPanel);

        main.SelectedPanelIndex = 1;
        Assert.IsType<SearchPanelViewModel>(main.CurrentPanel);

        main.SelectedPanelIndex = 2;
        Assert.IsType<AnalysisPanelViewModel>(main.CurrentPanel);

        main.SelectedPanelIndex = 3;
        Assert.IsType<SettingsViewModel>(main.CurrentPanel);

        main.SelectedPanelIndex = MainViewModel.NoPanel;
        Assert.Null(main.CurrentPanel);
    }

    [AvaloniaFact]
    public async Task SettingsPanel_ThemeAndCacheChanged_AreStoredAndApplied()
    {
        var main = _services.GetRequiredService<MainViewModel>();
        await main.Initialization;
        var settingsPanel = _services.GetRequiredService<SettingsViewModel>();

        settingsPanel.SelectedTheme = settingsPanel.Themes.Single(choice => choice.Value == AppTheme.Dark);
        settingsPanel.CacheMegabytes = 512;
        settingsPanel.SmoothMotion = false;

        var settings = _services.GetRequiredService<ISettingsService>().Current;
        Assert.Equal(AppTheme.Dark, settings.Appearance.Theme);
        Assert.Equal(512, settings.Map.CacheMegabytes);
        Assert.False(main.Map.SmoothMotion);
    }

    private RecordingNotificationService Notifications => (RecordingNotificationService)_services.GetRequiredService<INotificationService>();
}

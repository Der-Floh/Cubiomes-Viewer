using Avalonia.Controls.Notifications;
using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Repository.SettingsRepo;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Service.AppAddressServ;
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
        Assert.IsType<ExportViewModel>(main.CurrentPanel);

        main.SelectedPanelIndex = 4;
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

    [AvaloniaFact]
    public async Task Initialization_UnreadableStoredSettings_StartsWithTheDefaultsAndNamesTheCopy()
    {
        var store = _services.GetRequiredService<IAppDataStore>();
        await store.WriteAsync(SettingsRepository.StorageKey, "{ \"appearance\": { \"theme\": \"Sepia\" } }"u8.ToArray(), TestContext.Current.CancellationToken);

        var main = _services.GetRequiredService<MainViewModel>();
        await main.Initialization;

        Assert.Empty(Notifications.Errors);
        Assert.Equal(AppTheme.System, _services.GetRequiredService<ISettingsService>().Current.Appearance.Theme);
        var warning = Assert.Single(Notifications.Notifications, notification => notification.Type == NotificationType.Warning);
        Assert.Equal(main.UnreadableTitle, warning.Title);
        Assert.Contains("settings.unreadable-", warning.Message, StringComparison.Ordinal);
    }

    [AvaloniaFact]
    public async Task Initialization_StartLink_OpensItsViewOverTheRestoredOneAndRemembersTheSeed()
    {
        await StoreViewAsync(new ViewSettings { Seed = 1, Version = McVersion.V1_18_2, CenterX = 7, CenterZ = 7 });
        Address.StartAddress = $"{RecordingAppAddressService.TestLinkBase}?seed=262&version=1.16&dimension=nether&x=100&z=-50&scale=2&layer=height";

        var main = _services.GetRequiredService<MainViewModel>();
        await main.Initialization;

        var session = _services.GetRequiredService<WorldSession>();
        Assert.Empty(Notifications.Notifications);
        Assert.Equal(262, session.Seed);
        Assert.Equal(McVersion.V1_16_5, session.Version);
        Assert.Equal(Dimension.Nether, session.Dimension);
        Assert.Equal(new MapCamera(100.5, -49.5, 2), main.Map.Camera);
        Assert.Equal(262L, _services.GetRequiredService<ISeedHistoryService>().Seeds[0]);
        Assert.Equal(262, _services.GetRequiredService<ISettingsService>().Current.View.Seed);
    }

    [AvaloniaFact]
    public async Task Initialization_StartLinkWithUnreadableValues_OpensTheRestAndWarnsOnce()
    {
        await StoreViewAsync(new ViewSettings { Seed = 1, Version = McVersion.V1_18_2 });
        Address.StartAddress = $"{RecordingAppAddressService.TestLinkBase}?seed=262&version=1.99";

        var main = _services.GetRequiredService<MainViewModel>();
        await main.Initialization;

        var session = _services.GetRequiredService<WorldSession>();
        Assert.Equal(262, session.Seed);
        Assert.Equal(McVersion.V1_18_2, session.Version);
        var warning = Assert.Single(Notifications.Notifications);
        Assert.Equal(NotificationType.Warning, warning.Type);
        Assert.Equal("The link's version could not be read and stays as it is.", warning.Message);
    }

    [AvaloniaFact]
    public async Task Initialization_StartAddressWithoutLink_RestoresTheStoredViewAndShowsItsLink()
    {
        await StoreViewAsync(new ViewSettings { Seed = 262, Version = McVersion.V1_21_3, CenterX = 10.2, CenterZ = -3.7, BlocksPerPixel = 2 });
        Address.StartAddress = RecordingAppAddressService.TestLinkBase;

        var main = _services.GetRequiredService<MainViewModel>();
        await main.Initialization;

        Assert.Empty(Notifications.Notifications);
        Assert.Equal(262, _services.GetRequiredService<WorldSession>().Seed);
        Assert.Equal($"{RecordingAppAddressService.TestLinkBase}?seed=262&version=1.21.3&dimension=overworld&x=10&z=-4&scale=2", Assert.Single(Address.Shown));
    }

    [AvaloniaFact]
    public async Task ViewChanges_AfterInitialization_AreShownInTheAddressBar()
    {
        var main = _services.GetRequiredService<MainViewModel>();
        await main.Initialization;

        _services.GetRequiredService<WorldSession>().Dimension = Dimension.End;
        main.Map.Camera = new MapCamera(-20.5, 30, 8);

        Assert.EndsWith("&dimension=end&x=-21&z=30&scale=8", Address.Shown[^1], StringComparison.Ordinal);
    }

    private async Task StoreViewAsync(ViewSettings view)
        => await _services.GetRequiredService<ISettingsRepository>().SaveAsync(new AppSettings { View = view }, TestContext.Current.CancellationToken);

    private RecordingNotificationService Notifications => (RecordingNotificationService)_services.GetRequiredService<INotificationService>();

    private RecordingAppAddressService Address => (RecordingAppAddressService)_services.GetRequiredService<IAppAddressService>();
}

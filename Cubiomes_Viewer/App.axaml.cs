using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

using Cubiomes_Viewer.Controls.Map;
using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Service.ClipboardServ;
using Cubiomes_Viewer.Service.MapStyleServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.StorageServ;
using Cubiomes_Viewer.Service.ThemeServ;
using Cubiomes_Viewer.Service.TileBitmapServ;
using Cubiomes_Viewer.Service.TopLevelServ;
using Cubiomes_Viewer.ViewModels;
using Cubiomes_Viewer.Views;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer;

public sealed partial class App : Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    /// <summary>
    /// Set by a platform head before the application starts to replace or add platform-specific services.
    /// </summary>
    public static Action<IServiceCollection>? ConfigurePlatformServices { get; set; }

    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var services = new ServiceCollection();
        ConfigureServices(services);
        Services = services.BuildServiceProvider();

        SetupGlobalExceptionHandlers();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = Services.GetRequiredService<MainWindow>();
            desktop.Exit += (_, _) => Services.GetRequiredService<ISettingsService>().FlushAsync().GetAwaiter().GetResult();
        }
        else if (ApplicationLifetime is IActivityApplicationLifetime activity)
        {
            activity.MainViewFactory = () => Services.GetRequiredService<MainContainer>();
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = Services.GetRequiredService<MainContainer>();
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static void SetupGlobalExceptionHandlers()
    {
        var logger = Services.GetRequiredService<ILogger<App>>();

        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
        {
            if (e.ExceptionObject is not Exception ex)
                return;

            logger.LogError(ex, "Unhandled exception");
            Services.GetService<INotificationService>()?.ShowError(ex.Message);
        };

        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.LogError(e.Exception, "Unobserved task exception");
            Services.GetService<INotificationService>()?.ShowError(e.Exception.InnerException?.Message ?? e.Exception.Message);
            e.SetObserved();
        };
    }

    internal static void ConfigureServices(IServiceCollection services)
    {
#if DEBUG
        var minLevel = LogLevel.Debug;
#else
        var minLevel = LogLevel.Warning;
#endif
        services.AddLogging(builder =>
        {
            // AddConsole() uses a background thread queue which throws PlatformNotSupportedException
            // in single-threaded WASM. Use a simple synchronous console logger for Browser instead.
            if (OperatingSystem.IsBrowser())
                builder.AddProvider(new SyncConsoleLoggerProvider());
            else
                builder.AddConsole();

            builder.SetMinimumLevel(minLevel);
        });

        services.AddCubiomesEngine();

        // services
        services.AddSingleton<ITopLevelProvider, TopLevelProvider>();
        services.AddSingleton<IStorageService, StorageService>();
        services.AddSingleton<INotificationService, NotificationService>();
        services.AddSingleton<IThemeService, ThemeService>();
        services.AddSingleton<ITileBitmapService, TileBitmapService>();
        services.AddSingleton<IMapStyleService, MapStyleService>();
        services.AddSingleton<IClipboardService, ClipboardService>();
        services.AddSingleton<WorldSession>();

        // map layers
        services.AddTransient<TileLayer>();
        services.AddTransient<SlimeChunkLayer>();
        services.AddTransient<GridLayer>();
        services.AddTransient<StructureLayer>();
        services.AddTransient<LandmarkLayer>();
        services.AddTransient<MeasureLayer>();

        // views
        services.AddTransient<MainWindow>();
        services.AddTransient<MainContainer>();
        services.AddTransient<DesktopShellView>();
        services.AddTransient<MobileShellView>();
        services.AddTransient<MapHostView>();
        services.AddTransient<LayersPanelView>();
        services.AddTransient<SettingsView>();

        // view models
        services.AddSingleton<MainViewModel>();
        services.AddSingleton<WorldBarViewModel>();
        services.AddSingleton<MapViewModel>();
        services.AddSingleton<MapMenuViewModel>();
        services.AddSingleton<MapHostViewModel>();
        services.AddSingleton<LayersPanelViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddTransient<SelfTestViewModel>();

        ConfigurePlatformServices?.Invoke(services);
    }

    private sealed class SyncConsoleLoggerProvider : ILoggerProvider
    {
        public ILogger CreateLogger(string categoryName) => new SyncConsoleLogger(categoryName);
        public void Dispose() { }
    }

    private sealed class SyncConsoleLogger(string categoryName) : ILogger
    {
        public bool IsEnabled(LogLevel logLevel) => true;
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
        {
            Console.WriteLine($"[{logLevel}] {categoryName}: {formatter(state, exception)}");
            if (exception is not null)
                Console.WriteLine(exception.ToString());
        }
    }
}

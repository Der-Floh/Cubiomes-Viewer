using Avalonia;

using Cubiomes_Viewer.Desktop.Service.UpdateServ;
using Cubiomes_Viewer.Service.UpdateServ;

using Microsoft.Extensions.DependencyInjection;

using Velopack;

namespace Cubiomes_Viewer.Desktop;

internal sealed class Program
{
    // Initialization code. Don't use any Avalonia, third-party APIs or any
    // SynchronizationContext-reliant code before AppMain is called: things aren't initialized
    // yet and stuff might break.
    [STAThread]
    public static void Main(string[] args)
    {
        VelopackApp.Build().Run();
        App.ConfigurePlatformServices = services => services.AddSingleton<IUpdateService, VelopackUpdateService>();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    // Avalonia configuration, don't remove; also used by visual designer.
    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}

using Avalonia;
using Avalonia.Headless;

using Cubiomes_Viewer.Tests;

[assembly: AvaloniaTestApplication(typeof(TestAppBuilder))]

namespace Cubiomes_Viewer.Tests;

public static class TestAppBuilder
{
    public static AppBuilder BuildAvaloniaApp()
    {
        App.ConfigurePlatformServices = TestServices.UseIsolatedPlatform;

        return AppBuilder.Configure<App>()
            .UseSkia()
            .UseHarfBuzz()
            .UseHeadless(new AvaloniaHeadlessPlatformOptions { UseHeadlessDrawing = false });
    }
}

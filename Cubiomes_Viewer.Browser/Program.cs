using Avalonia;
using Avalonia.Browser;
using Avalonia.Logging;

using Cubiomes_Viewer;
using Cubiomes_Viewer.Browser.Service;

internal sealed partial class Program
{
    private static Task Main(string[] args)
    {
        if (args.Any(arg => arg.Contains(WorkerDiagnostics.UrlSwitch, StringComparison.OrdinalIgnoreCase)))
            _ = WorkerDiagnostics.RunAsync();

        return BuildAvaloniaApp()
            .WithInterFont()
#if DEBUG
            .WithDeveloperTools()
            .LogToTextWriter(Console.Out, LogEventLevel.Warning)
#endif
            .StartBrowserAppAsync("out");
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>();
}

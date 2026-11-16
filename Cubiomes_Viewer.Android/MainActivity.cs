using Android.App;
using Android.Content.PM;

using Avalonia.Android;

namespace Cubiomes_Viewer.Android;

[Activity(
    Label = "Cubiomes Viewer",
    Theme = "@style/Theme.CubiomesViewer",
    Icon = "@mipmap/ic_launcher",
    MainLauncher = true,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.ScreenSize | ConfigChanges.SmallestScreenSize | ConfigChanges.ScreenLayout
        | ConfigChanges.UiMode | ConfigChanges.Density)]
public sealed class MainActivity : AvaloniaMainActivity
{
    /// <summary>Writes what is waiting to be stored, as Android may end the app at any time once it is in the background.</summary>
    protected override void OnStop()
    {
        App.FlushStoredStateAsync().GetAwaiter().GetResult();
        base.OnStop();
    }
}

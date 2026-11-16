using Android.App;
using Android.Runtime;

using Avalonia;
using Avalonia.Android;

using Cubiomes_Viewer.Android.Service.UpdateServ;
using Cubiomes_Viewer.Service.UpdateServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Android;

[Application]
public sealed class MainApplication(nint javaReference, JniHandleOwnership transfer) : AvaloniaAndroidApplication<App>(javaReference, transfer)
{
    public override void OnCreate()
    {
        App.ConfigurePlatformServices = services => services.AddSingleton<IUpdateService, AndroidUpdateService>();
        base.OnCreate();
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
        => base.CustomizeAppBuilder(builder)
            .WithInterFont();
}

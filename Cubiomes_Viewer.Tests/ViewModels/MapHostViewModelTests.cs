using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Service.MapStyleServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class MapHostViewModelTests : IDisposable
{
    private readonly ServiceProvider _services = TestServices.Create();

    public void Dispose() => _services.Dispose();

    [AvaloniaFact]
    public void Dismiss_WhileMeasuringWithAPickedMarker_EndsTheMeasurementFirstAndThenClosesTheCard()
    {
        var host = _services.GetRequiredService<MapHostViewModel>();
        var marker = _services.GetRequiredService<IMapStyleService>().Spawn;
        host.Map.Selection = new MapSelection("World spawn", marker, new BlockPos(8, 8));
        host.Map.MeasureStart = new BlockPos(0, 0);

        host.DismissCommand.Execute(null);
        Assert.False(host.Map.IsMeasuring);
        Assert.True(host.Selection.IsOpen);

        host.DismissCommand.Execute(null);
        Assert.Null(host.Map.Selection);
        Assert.False(host.Selection.IsOpen);
    }
}

using System.Runtime.InteropServices.JavaScript;

using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Browser.Worker;

internal static partial class WorkerExports
{
    private static readonly ServiceProvider _services = new ServiceCollection()
        .AddLogging()
        .AddCubiomesEngine()
        .BuildServiceProvider();

    private static readonly ComputeWorkerHost _host = _services.GetRequiredService<ComputeWorkerHost>();

    /// <summary>Executes one serialized compute job and returns the serialized response.</summary>
    [JSExport]
    internal static byte[] Execute(byte[] request) => _host.Execute(request);
}

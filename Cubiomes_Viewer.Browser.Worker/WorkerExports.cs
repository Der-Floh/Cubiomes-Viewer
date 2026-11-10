using System.Runtime.InteropServices.JavaScript;

using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.SelfTestServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Browser.Worker;

internal static partial class WorkerExports
{
    private const int TileHeight = 16;

    private static readonly ServiceProvider _services = new ServiceCollection()
        .AddLogging()
        .AddCubiomesEngine()
        .BuildServiceProvider();

    [JSExport]
    internal static string RunSelfTest()
        => _services.GetRequiredService<ISelfTestService>().Run().ToString();

    [JSExport]
    internal static byte[] GenerateBiomeTile(int seed, int scale, int size)
    {
        var versions = _services.GetRequiredService<IVersionCatalog>();
        using var generator = _services.GetRequiredService<IWorldGeneratorFactory>().Create(versions.Newest, GeneratorFlags.None);
        generator.Apply(Dimension.Overworld, seed);

        var area = new BiomeArea(scale, -size / 2, -size / 2, size, size, TileHeight);
        var biomes = new byte[area.SampleCount];
        generator.GenerateBiomes(area, biomes);
        return biomes;
    }
}

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Loot;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.LootServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Engine.Tests.Service.LootServ;

public sealed class StructureLootJobHandlerTests : IDisposable
{
    private static readonly WorldKey _end = new(262, McVersion.V1_21_3, Dimension.End, GeneratorFlags.None);
    private static readonly BlockArea _area = new(-4096, -4096, 8192, 8192);

    private readonly ServiceProvider _services = new ServiceCollection().AddLogging().AddCubiomesEngine().BuildServiceProvider();

    public void Dispose() => _services.Dispose();

    [Fact]
    public void WorkerHost_LootJob_ReturnsWhatItsHandlerRolls()
    {
        var host = _services.GetRequiredService<ComputeWorkerHost>();
        using var context = new WorkerContext(new CubiomesWorldGeneratorFactory());
        var city = context.GetGenerator(_end).FindStructures(StructureType.EndCity, _area)[0];
        var job = new StructureLootJob(_end, StructureType.EndCity, city);

        var loot = ComputeEnvelope.DeserializeResponse<StructureLoot>(host.Execute(ComputeEnvelope.SerializeJob(job)));

        var expected = _services.GetRequiredService<StructureLootJobHandler>().Handle(job, context, CancellationToken.None);
        Assert.True(loot.IsKnown);
        Assert.NotEmpty(loot.Containers);
        Assert.Equal(LootText.Describe(expected), LootText.Describe(loot));
    }

    [Fact]
    public void Handle_StructureWithoutLoot_IsUnknown()
    {
        using var context = new WorkerContext(new CubiomesWorldGeneratorFactory());
        var overworld = _end with { Dimension = Dimension.Overworld };
        var village = context.GetGenerator(overworld).FindStructures(StructureType.Village, _area)[0];

        var loot = new StructureLootJobHandler().Handle(new StructureLootJob(overworld, StructureType.Village, village), context, CancellationToken.None);

        Assert.False(loot.IsKnown);
        Assert.Empty(loot.Containers);
    }
}

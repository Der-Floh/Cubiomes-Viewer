using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.ConditionServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

/// <summary>Evaluates single conditions against real worlds and hands out the generator of the same world to compare with.</summary>
internal sealed class ConditionTestBench : IDisposable
{
    public static BlockPos Origin { get; } = new(0, 0);

    public ConditionRegistry Registry { get; } = TestEngine.CreateConditionRegistry();

    private readonly WorkerContext _workerContext = new(new CubiomesWorldGeneratorFactory());

    public (MatchStatus Status, List<BlockPos> Matches) Evaluate(SearchCondition condition, McVersion version, long seed, SearchPass pass, BlockPos reference = default)
    {
        List<BlockPos> matches = [];
        var status = Registry.GetEvaluator(condition).Evaluate(condition, reference, Open(Define(version, condition), seed), pass, matches);
        return (status, matches);
    }

    public SearchSeedContext Open(SearchDefinition definition, long seed)
    {
        var context = new SearchSeedContext(_workerContext, definition);
        context.Open(seed);
        return context;
    }

    public IWorldGenerator GetGenerator(McVersion version, long seed, Dimension dimension = Dimension.Overworld)
        => _workerContext.GetGenerator(new WorldKey(seed, version, dimension, new SearchDefinition().Flags));

    public static SearchDefinition Define(McVersion version, params SearchCondition[] conditions) => new() { Version = version, Conditions = conditions };

    public void Dispose() => _workerContext.Dispose();
}

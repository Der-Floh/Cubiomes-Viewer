using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

public sealed class WorkerContext : IWorkerContext, IDisposable
{
    /// <summary>Structure rules change while someone edits salts; the generators of older rules are let go.</summary>
    private const int KeptRuleSets = 3;

    private readonly IWorldGeneratorFactory _generatorFactory;
    private readonly Dictionary<(McVersion Version, GeneratorFlags Flags, Dimension Dimension, StructureRules Rules), SeededGenerator> _generators = [];
    private readonly List<StructureRules> _recentRules = [];

    public WorkerContext(IWorldGeneratorFactory generatorFactory)
    {
        _generatorFactory = generatorFactory;
    }

    public IWorldGenerator GetGenerator(WorldKey world)
    {
        var key = (world.Version, world.Flags, world.Dimension, world.Rules);
        if (!_generators.TryGetValue(key, out var seeded))
        {
            Remember(world.Rules);
            seeded = new SeededGenerator(_generatorFactory.Create(world.Version, world.Flags, world.Rules), world.Dimension);
            _generators.Add(key, seeded);
        }

        seeded.Apply(world.Seed);
        return seeded.Generator;
    }

    public void Dispose()
    {
        foreach (var seeded in _generators.Values)
            seeded.Generator.Dispose();
        _generators.Clear();
        _recentRules.Clear();
    }

    /// <summary>Keeps the generators of the most recent rules; a job uses the rules of one world, which are always among them.</summary>
    private void Remember(StructureRules rules)
    {
        _recentRules.Remove(rules);
        _recentRules.Add(rules);
        if (_recentRules.Count <= KeptRuleSets)
            return;

        var oldest = _recentRules[0];
        _recentRules.RemoveAt(0);
        foreach (var key in _generators.Keys.Where(key => key.Rules.Equals(oldest)).ToList())
        {
            _generators[key].Generator.Dispose();
            _generators.Remove(key);
        }
    }

    /// <summary>One generator per dimension, so that work which alternates between dimensions seeds each of them once.</summary>
    private sealed class SeededGenerator(IWorldGenerator generator, Dimension dimension)
    {
        private long? _seed;

        public IWorldGenerator Generator { get; } = generator;

        public void Apply(long seed)
        {
            if (_seed == seed)
                return;

            Generator.Apply(dimension, seed);
            _seed = seed;
        }
    }
}

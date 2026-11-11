using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

public sealed class WorkerContext : IWorkerContext, IDisposable
{
    private readonly IWorldGeneratorFactory _generatorFactory;
    private readonly Dictionary<(McVersion Version, GeneratorFlags Flags, Dimension Dimension), SeededGenerator> _generators = [];

    public WorkerContext(IWorldGeneratorFactory generatorFactory)
    {
        _generatorFactory = generatorFactory;
    }

    public IWorldGenerator GetGenerator(WorldKey world)
    {
        var key = (world.Version, world.Flags, world.Dimension);
        if (!_generators.TryGetValue(key, out var seeded))
        {
            seeded = new SeededGenerator(_generatorFactory.Create(world.Version, world.Flags), world.Dimension);
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

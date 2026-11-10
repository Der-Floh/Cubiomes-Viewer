using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

public sealed class WorkerContext : IWorkerContext, IDisposable
{
    private readonly IWorldGeneratorFactory _generatorFactory;
    private readonly Dictionary<(McVersion Version, GeneratorFlags Flags), SeededGenerator> _generators = [];

    public WorkerContext(IWorldGeneratorFactory generatorFactory)
    {
        _generatorFactory = generatorFactory;
    }

    public IWorldGenerator GetGenerator(WorldKey world)
    {
        var key = (world.Version, world.Flags);
        if (!_generators.TryGetValue(key, out var seeded))
        {
            seeded = new SeededGenerator(_generatorFactory.Create(world.Version, world.Flags));
            _generators.Add(key, seeded);
        }

        seeded.Apply(world.Dimension, world.Seed);
        return seeded.Generator;
    }

    public void Dispose()
    {
        foreach (var seeded in _generators.Values)
            seeded.Generator.Dispose();
        _generators.Clear();
    }

    private sealed class SeededGenerator(IWorldGenerator generator)
    {
        private (Dimension Dimension, long Seed)? _applied;

        public IWorldGenerator Generator { get; } = generator;

        public void Apply(Dimension dimension, long seed)
        {
            if (_applied == (dimension, seed))
                return;

            Generator.Apply(dimension, seed);
            _applied = (dimension, seed);
        }
    }
}

using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>
/// The seed a worker is testing, the generators it tests it with and buffers the conditions fill while they look.
/// One context serves one worker; it is not safe to share.
/// </summary>
public sealed class SearchSeedContext
{
    public long Seed { get; private set; }
    public McVersion Version { get; }
    public GeneratorFlags Flags { get; }

    private readonly IWorkerContext _workerContext;
    private BlockPos[] _positions = [];
    private QuadPosition[] _quads = [];
    private byte[] _bytes = [];
    private short[] _values = [];

    public SearchSeedContext(IWorkerContext workerContext, SearchDefinition definition)
    {
        ArgumentNullException.ThrowIfNull(workerContext);
        ArgumentNullException.ThrowIfNull(definition);

        _workerContext = workerContext;
        Version = definition.Version;
        Flags = definition.Flags;
    }

    /// <summary>Moves on to another seed.</summary>
    public void Open(long seed) => Seed = seed;

    /// <summary>Returns the generator of one dimension of the world of the current seed.</summary>
    public IWorldGenerator GetGenerator(Dimension dimension) => _workerContext.GetGenerator(new WorldKey(Seed, Version, dimension, Flags));

    /// <summary>Lends a buffer for positions that stays valid until the next request.</summary>
    public Span<BlockPos> GetPositions(int count)
    {
        if (_positions.Length < count)
            _positions = new BlockPos[count];
        return _positions.AsSpan(0, count);
    }

    /// <summary>Lends a buffer for quads that stays valid until the next request.</summary>
    public Span<QuadPosition> GetQuads(int count)
    {
        if (_quads.Length < count)
            _quads = new QuadPosition[count];
        return _quads.AsSpan(0, count);
    }

    /// <summary>Lends a buffer for bytes that stays valid until the next request.</summary>
    public Span<byte> GetBytes(int count)
    {
        if (_bytes.Length < count)
            _bytes = new byte[count];
        return _bytes.AsSpan(0, count);
    }

    /// <summary>Lends a buffer for sampled values that stays valid until the next request.</summary>
    public Span<short> GetValues(int count)
    {
        if (_values.Length < count)
            _values = new short[count];
        return _values.AsSpan(0, count);
    }
}

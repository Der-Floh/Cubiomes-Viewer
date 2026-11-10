using System.Diagnostics.CodeAnalysis;

using Cubiomes_Viewer.Engine.Data.Maps;

namespace Cubiomes_Viewer.Engine.Service.TileServ;

/// <summary>A thread-safe least-recently-used cache of tile data with a byte budget.</summary>
public sealed class TileCache
{
    public long BudgetBytes
    {
        get => Interlocked.Read(ref _budgetBytes);
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            lock (_gate)
            {
                _budgetBytes = value;
                Trim(keep: null);
            }
        }
    }

    public long UsedBytes
    {
        get
        {
            lock (_gate)
                return _usedBytes;
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
                return _nodes.Count;
        }
    }

    private readonly Dictionary<TileRequest, LinkedListNode<TileData>> _nodes = [];
    private readonly LinkedList<TileData> _recency = new();
    private readonly object _gate = new();
    private long _budgetBytes;
    private long _usedBytes;

    public TileCache(long budgetBytes)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(budgetBytes);
        _budgetBytes = budgetBytes;
    }

    public bool TryGet(TileRequest request, [NotNullWhen(true)] out TileData? tile)
    {
        lock (_gate)
        {
            if (!_nodes.TryGetValue(request, out var node))
            {
                tile = null;
                return false;
            }

            _recency.Remove(node);
            _recency.AddFirst(node);
            tile = node.Value;
            return true;
        }
    }

    public bool Contains(TileRequest request)
    {
        lock (_gate)
            return _nodes.ContainsKey(request);
    }

    /// <summary>Adds or replaces a tile and evicts the least recently used tiles beyond the budget; the added tile itself is never evicted.</summary>
    public void Add(TileData tile)
    {
        ArgumentNullException.ThrowIfNull(tile);

        lock (_gate)
        {
            if (_nodes.Remove(tile.Request, out var existing))
            {
                _recency.Remove(existing);
                _usedBytes -= existing.Value.ByteSize;
            }

            var node = _recency.AddFirst(tile);
            _nodes.Add(tile.Request, node);
            _usedBytes += tile.ByteSize;
            Trim(keep: node);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _nodes.Clear();
            _recency.Clear();
            _usedBytes = 0;
        }
    }

    private void Trim(LinkedListNode<TileData>? keep)
    {
        while (_usedBytes > _budgetBytes && _recency.Last is { } last && last != keep)
        {
            _recency.RemoveLast();
            _nodes.Remove(last.Value.Request);
            _usedBytes -= last.Value.ByteSize;
        }
    }
}

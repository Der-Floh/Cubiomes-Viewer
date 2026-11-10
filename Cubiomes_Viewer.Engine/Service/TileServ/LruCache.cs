using System.Diagnostics.CodeAnalysis;

namespace Cubiomes_Viewer.Engine.Service.TileServ;

/// <summary>A thread-safe least-recently-used cache whose entries count against a size budget.</summary>
public sealed class LruCache<TKey, TValue>
    where TKey : notnull
{
    public long Budget
    {
        get => Interlocked.Read(ref _budget);
        set
        {
            ArgumentOutOfRangeException.ThrowIfNegative(value);
            lock (_gate)
            {
                _budget = value;
                Trim(keep: null);
            }
        }
    }

    public long UsedSize
    {
        get
        {
            lock (_gate)
                return _usedSize;
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

    private readonly Func<TValue, long> _getSize;
    private readonly Dictionary<TKey, LinkedListNode<Entry>> _nodes = [];
    private readonly LinkedList<Entry> _recency = new();
    private readonly Lock _gate = new();
    private long _budget;
    private long _usedSize;

    /// <param name="budget">The total size the cache may hold, in the unit <paramref name="getSize"/> returns.</param>
    /// <param name="getSize">Measures one value; it is called once when the value is added.</param>
    public LruCache(long budget, Func<TValue, long> getSize)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(budget);
        ArgumentNullException.ThrowIfNull(getSize);

        _budget = budget;
        _getSize = getSize;
    }

    public bool TryGet(TKey key, [MaybeNullWhen(false)] out TValue value)
    {
        lock (_gate)
        {
            if (!_nodes.TryGetValue(key, out var node))
            {
                value = default;
                return false;
            }

            _recency.Remove(node);
            _recency.AddFirst(node);
            value = node.Value.Value;
            return true;
        }
    }

    public bool Contains(TKey key)
    {
        lock (_gate)
            return _nodes.ContainsKey(key);
    }

    /// <summary>Adds or replaces a value and evicts the least recently used values beyond the budget; the added value itself is never evicted.</summary>
    public void Add(TKey key, TValue value)
    {
        var entry = new Entry(key, value, _getSize(value));
        lock (_gate)
        {
            if (_nodes.Remove(key, out var existing))
            {
                _recency.Remove(existing);
                _usedSize -= existing.Value.Size;
            }

            var node = _recency.AddFirst(entry);
            _nodes.Add(key, node);
            _usedSize += entry.Size;
            Trim(keep: node);
        }
    }

    public void Clear()
    {
        lock (_gate)
        {
            _nodes.Clear();
            _recency.Clear();
            _usedSize = 0;
        }
    }

    private void Trim(LinkedListNode<Entry>? keep)
    {
        while (_usedSize > _budget && _recency.Last is { } last && last != keep)
        {
            _recency.RemoveLast();
            _nodes.Remove(last.Value.Key);
            _usedSize -= last.Value.Size;
        }
    }

    private readonly record struct Entry(TKey Key, TValue Value, long Size);
}

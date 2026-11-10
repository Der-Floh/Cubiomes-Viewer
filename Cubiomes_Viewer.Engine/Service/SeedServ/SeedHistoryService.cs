namespace Cubiomes_Viewer.Engine.Service.SeedServ;

public sealed class SeedHistoryService : ISeedHistoryService
{
    public const int Capacity = 16;

    public event Action? Changed;

    public IReadOnlyList<long> Seeds => _seeds;

    private readonly List<long> _seeds = [];

    public void Load(IEnumerable<long> seeds)
    {
        ArgumentNullException.ThrowIfNull(seeds);

        _seeds.Clear();
        _seeds.AddRange(seeds.Distinct().Take(Capacity));
        Changed?.Invoke();
    }

    public void Add(long seed)
    {
        if (_seeds.Count > 0 && _seeds[0] == seed)
            return;

        _seeds.Remove(seed);
        _seeds.Insert(0, seed);
        if (_seeds.Count > Capacity)
            _seeds.RemoveRange(Capacity, _seeds.Count - Capacity);
        Changed?.Invoke();
    }
}

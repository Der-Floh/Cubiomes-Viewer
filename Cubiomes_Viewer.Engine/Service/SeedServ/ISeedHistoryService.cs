namespace Cubiomes_Viewer.Engine.Service.SeedServ;

/// <summary>Remembers the most recently opened seeds, newest first.</summary>
public interface ISeedHistoryService
{
    event Action? Changed;

    IReadOnlyList<long> Seeds { get; }

    void Load(IEnumerable<long> seeds);
    void Add(long seed);
}

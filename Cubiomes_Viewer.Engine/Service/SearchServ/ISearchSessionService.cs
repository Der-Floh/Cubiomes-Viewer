using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Service.SearchServ;

/// <summary>
/// Keeps what the search is set up to look for and the search that ran last, and stores both shortly after they
/// change. Nothing is stored before a successful <see cref="LoadAsync"/>, so a session that could not be read is
/// never overwritten.
/// </summary>
public interface ISearchSessionService
{
    /// <summary>What the search is set up to look for. Assigning it schedules a save.</summary>
    SearchSetup Setup { get; set; }

    /// <summary>Loads the stored session and hands its search to the search service, paused where it stopped.</summary>
    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>Writes pending changes immediately.</summary>
    Task FlushAsync(CancellationToken cancellationToken = default);

    /// <summary>Describes the setup and the last search as they are now.</summary>
    SearchSession CreateSession();

    /// <summary>Replaces the setup and the last search with the ones of another session.</summary>
    /// <exception cref="InvalidOperationException">A search is running.</exception>
    void Open(SearchSession session);
}

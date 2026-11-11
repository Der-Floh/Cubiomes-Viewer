using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Service.SearchServ;

/// <summary>
/// Scans seeds for the ones that meet a definition. It holds one search at a time and is meant to be used from one
/// thread: it reports back on the context a search was started from.
/// </summary>
public interface ISeedSearchService
{
    SearchState State { get; }

    /// <summary>The search that is running or ran last.</summary>
    SearchSetup? Setup { get; }

    SearchProgress Progress { get; }

    /// <summary>The seeds found so far, in ascending order. They are the first ones from the start of the search on.</summary>
    IReadOnlyList<long> Results { get; }

    /// <summary>Raised when the state, the progress or the results changed.</summary>
    event EventHandler? Changed;

    /// <summary>
    /// Starts a new search and completes when it stops: when it is finished, or paused because
    /// <paramref name="cancellationToken"/> was cancelled.
    /// </summary>
    /// <exception cref="InvalidSearchException">The definition cannot be searched.</exception>
    /// <exception cref="InvalidOperationException">A search is running.</exception>
    Task StartAsync(SearchSetup setup, CancellationToken cancellationToken = default);

    /// <summary>Continues a paused search where it stopped, and completes like <see cref="StartAsync"/>.</summary>
    /// <exception cref="InvalidOperationException">No search is paused.</exception>
    Task ResumeAsync(CancellationToken cancellationToken = default);

    /// <summary>Describes the search so that it can be stored, or returns <see langword="null"/> when there is none.</summary>
    SearchSnapshot? CreateSnapshot();

    /// <summary>Takes over a stored search: paused where it stopped, or finished.</summary>
    /// <exception cref="InvalidOperationException">A search is running.</exception>
    void Restore(SearchSnapshot snapshot);

    /// <summary>Forgets the search and its results.</summary>
    /// <exception cref="InvalidOperationException">A search is running.</exception>
    void Reset();
}

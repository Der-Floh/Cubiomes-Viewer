using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Service.SearchServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Cli.Tests;

/// <summary>
/// Searches a session file as the app does after "Open file": its Search panel offers to continue a stopped search
/// whose setup is unchanged and otherwise starts the setup over.
/// </summary>
internal static class AppSearch
{
    public static async Task<AppSearchResult> RunAsync(string sessionPath, string appData, CancellationToken cancellationToken)
    {
        await using var services = new ServiceCollection()
            .AddLogging()
            .AddCubiomesEngine()
            .AddSingleton<IAppDataStore>(new FileSystemAppDataStore(appData))
            .BuildServiceProvider();
        var sessions = services.GetRequiredService<ISearchSessionService>();
        var search = services.GetRequiredService<ISeedSearchService>();
        sessions.Import(await File.ReadAllBytesAsync(sessionPath, cancellationToken));

        var canContinue = search.State == SearchState.Paused && search.Setup == sessions.Setup;
        await (canContinue ? search.ResumeAsync(cancellationToken) : search.StartAsync(sessions.Setup, cancellationToken));
        return new AppSearchResult([.. search.Results], canContinue, search.State);
    }
}

/// <param name="Seeds">The seeds the app found.</param>
/// <param name="Continued">Whether the app continued a stopped search instead of starting over.</param>
/// <param name="State">Where the search stands at the end.</param>
internal sealed record AppSearchResult(IReadOnlyList<long> Seeds, bool Continued, SearchState State);

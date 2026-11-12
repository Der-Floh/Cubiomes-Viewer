using System.Text.Json;

using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Repository.SessionRepo;

public sealed class SearchSessionRepository : ISearchSessionRepository
{
    public const string StorageKey = "search-session.json";

    private const string NotASessionMessage = "This is not a search session of Cubiomes Viewer.";

    private readonly IAppDataStore _store;
    private readonly ILogger<SearchSessionRepository> _logger;

    public SearchSessionRepository(IAppDataStore store, ILogger<SearchSessionRepository> logger)
    {
        _store = store;
        _logger = logger;
    }

    public async Task<SearchSession?> LoadAsync(CancellationToken cancellationToken = default)
    {
        var content = await _store.ReadAsync(StorageKey, cancellationToken).ConfigureAwait(false);
        if (content is null)
            return null;

        try
        {
            return Deserialize(content);
        }
        catch (FormatException ex)
        {
            _logger.LogWarning(ex, "The stored search session is unreadable; starting without one");
            return null;
        }
    }

    public Task SaveAsync(SearchSession session, CancellationToken cancellationToken = default)
        => _store.WriteAsync(StorageKey, Serialize(session), cancellationToken);

    public byte[] Serialize(SearchSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        return JsonSerializer.SerializeToUtf8Bytes(session, SessionJsonContext.Default.SearchSession);
    }

    public SearchSession Deserialize(ReadOnlySpan<byte> content)
    {
        SearchSession? session;
        try
        {
            session = JsonSerializer.Deserialize(content, SessionJsonContext.Default.SearchSession);
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new FormatException(NotASessionMessage, ex);
        }

        if (session is null)
            throw new FormatException(NotASessionMessage);
        if (session.SchemaVersion > SearchSession.CurrentSchemaVersion)
            throw new FormatException("This search session was written by a newer version of Cubiomes Viewer.");
        return session;
    }
}

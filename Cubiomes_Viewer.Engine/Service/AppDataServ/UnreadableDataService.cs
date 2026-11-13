using System.Globalization;

using Cubiomes_Viewer.Engine.Repository.AppDataRepo;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Service.AppDataServ;

public sealed class UnreadableDataService : IUnreadableDataService
{
    private const string CopyMarker = ".unreadable-";
    private const string TimestampFormat = "yyyyMMdd-HHmmss";

    private readonly IAppDataStore _store;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<UnreadableDataService> _logger;
    private readonly Lock _gate = new();
    private readonly List<KeptCopy> _copies = [];

    public UnreadableDataService(IAppDataStore store, TimeProvider timeProvider, ILogger<UnreadableDataService> logger)
    {
        _store = store;
        _timeProvider = timeProvider;
        _logger = logger;
    }

    public IReadOnlyList<string> CopyKeys
    {
        get
        {
            lock (_gate)
                return [.. _copies.Select(copy => copy.CopyKey)];
        }
    }

    public async Task KeepAsync(UnreadableDataException unreadable, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(unreadable);
        if (IsKept(unreadable))
            return;

        var copyKey = GetCopyKey(unreadable.Key);
        await _store.WriteAsync(copyKey, unreadable.Content, cancellationToken).ConfigureAwait(false);
        lock (_gate)
            _copies.Add(new KeptCopy(unreadable.Key, copyKey, unreadable.Content.ToArray()));

        _logger.LogWarning(unreadable, "The stored {Key} cannot be read; it was copied to {CopyKey} and the app goes on without it", unreadable.Key, copyKey);
    }

    private bool IsKept(UnreadableDataException unreadable)
    {
        lock (_gate)
            return _copies.Any(copy => string.Equals(copy.Key, unreadable.Key, StringComparison.Ordinal) && copy.Content.AsSpan().SequenceEqual(unreadable.Content.Span));
    }

    private string GetCopyKey(string key)
    {
        var timestamp = _timeProvider.GetLocalNow().ToString(TimestampFormat, CultureInfo.InvariantCulture);
        return $"{Path.GetFileNameWithoutExtension(key)}{CopyMarker}{timestamp}{Path.GetExtension(key)}";
    }

    private sealed record KeptCopy(string Key, string CopyKey, byte[] Content);
}

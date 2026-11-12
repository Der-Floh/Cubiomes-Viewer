using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Repository.SessionRepo;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Service.SearchServ;

public sealed class SearchSessionService : ISearchSessionService, IDisposable
{
    private static readonly TimeSpan _saveDelay = TimeSpan.FromMilliseconds(750);
    private static readonly TimeSpan _runningSaveInterval = TimeSpan.FromSeconds(5);

    public SearchSetup Setup
    {
        get;
        set
        {
            ArgumentNullException.ThrowIfNull(value);
            if (value == field)
                return;

            field = value;
            RequestSave(_saveDelay, replacePending: true);
        }
    } = new() { Definition = new SearchDefinition() };

    private readonly ISearchSessionRepository _repository;
    private readonly ISeedSearchService _search;
    private readonly ILogger<SearchSessionService> _logger;
    private CancellationTokenSource? _pendingSave;
    private bool _isLoaded;

    public SearchSessionService(ISearchSessionRepository repository, ISeedSearchService search, ILogger<SearchSessionService> logger)
    {
        _repository = repository;
        _search = search;
        _logger = logger;
        _search.Changed += OnSearchChanged;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var session = await _repository.LoadAsync(cancellationToken);
            if (session is not null)
                Adopt(session);
            _isLoaded = true;
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "The search session could not be loaded; the search starts empty and is not stored");
        }
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        _pendingSave?.Cancel();
        _pendingSave = null;
        return _isLoaded ? _repository.SaveAsync(CreateSession(), cancellationToken) : Task.CompletedTask;
    }

    public SearchSession CreateSession() => new() { Setup = Setup, Search = _search.CreateSnapshot() };

    public void Open(SearchSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (_search.State == SearchState.Running)
            throw new InvalidOperationException("A search is running.");

        Adopt(session);
        RequestSave(_saveDelay, replacePending: true);
    }

    public byte[] Export() => _repository.Serialize(CreateSession());

    public void Import(ReadOnlySpan<byte> content) => Open(_repository.Deserialize(content));

    public void Dispose()
    {
        _search.Changed -= OnSearchChanged;
        _pendingSave?.Cancel();
    }

    private void Adopt(SearchSession session)
    {
        Setup = session.Setup;
        if (session.Search is not { } snapshot)
        {
            _search.Reset();
            return;
        }

        try
        {
            _search.Restore(snapshot);
        }
        catch (Exception ex) when (ex is InvalidSearchException or ArgumentException)
        {
            _logger.LogWarning(ex, "The stored search cannot be continued; only its setup is kept");
            _search.Reset();
        }
    }

    /// <summary>A running search changes all the time, so it is stored at intervals instead of after each change.</summary>
    private void OnSearchChanged(object? sender, EventArgs e)
    {
        if (_search.State == SearchState.Running)
            RequestSave(_runningSaveInterval, replacePending: false);
        else
            RequestSave(_saveDelay, replacePending: true);
    }

    private void RequestSave(TimeSpan delay, bool replacePending)
    {
        if (!_isLoaded || (_pendingSave is not null && !replacePending))
            return;

        _pendingSave?.Cancel();
        var pending = new CancellationTokenSource();
        _pendingSave = pending;
        _ = SaveAfterDelayAsync(pending, delay);
    }

    private async Task SaveAfterDelayAsync(CancellationTokenSource pending, TimeSpan delay)
    {
        try
        {
            await Task.Delay(delay, pending.Token);
            if (_pendingSave == pending)
                _pendingSave = null;

            await _repository.SaveAsync(CreateSession(), CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // A newer save request or a flush superseded this one.
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "The search session could not be saved");
        }
        finally
        {
            pending.Dispose();
        }
    }
}

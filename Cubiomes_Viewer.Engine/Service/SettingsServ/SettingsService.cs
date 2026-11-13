using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Repository.SettingsRepo;
using Cubiomes_Viewer.Engine.Service.AppDataServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Service.SettingsServ;

public sealed class SettingsService : ISettingsService
{
    private static readonly TimeSpan _saveDelay = TimeSpan.FromMilliseconds(750);

    public AppSettings Current { get; private set; } = new();

    private readonly ISettingsRepository _repository;
    private readonly IUnreadableDataService _unreadable;
    private readonly ILogger<SettingsService> _logger;
    private CancellationTokenSource? _pendingSave;
    private Task _saving = Task.CompletedTask;
    private bool _isLoaded;

    public SettingsService(ISettingsRepository repository, IUnreadableDataService unreadable, ILogger<SettingsService> logger)
    {
        _repository = repository;
        _unreadable = unreadable;
        _logger = logger;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Current = await ReadAsync(cancellationToken);
            _isLoaded = true;
            _logger.LogDebug("Settings loaded: schema={SchemaVersion}", Current.SchemaVersion);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Settings could not be loaded; the defaults are used and changes are not saved");
        }
    }

    public void RequestSave()
    {
        if (!_isLoaded)
            return;

        _pendingSave?.Cancel();
        var pending = new CancellationTokenSource();
        _pendingSave = pending;
        _ = SaveAfterDelayAsync(pending);
    }

    public async Task FlushAsync(CancellationToken cancellationToken = default)
    {
        _pendingSave?.Cancel();
        _pendingSave = null;
        if (!_isLoaded)
            return;

        await Task.WhenAny(_saving).ConfigureAwait(false);
        await _repository.SaveAsync(Current, cancellationToken).ConfigureAwait(false);
    }

    private async Task<AppSettings> ReadAsync(CancellationToken cancellationToken)
    {
        try
        {
            return await _repository.LoadAsync(cancellationToken);
        }
        catch (UnreadableDataException ex)
        {
            await _unreadable.KeepAsync(ex, cancellationToken);
            return new AppSettings();
        }
    }

    private async Task SaveAfterDelayAsync(CancellationTokenSource pending)
    {
        try
        {
            await Task.Delay(_saveDelay, pending.Token);
            if (_pendingSave == pending)
                _pendingSave = null;

            _saving = _repository.SaveAsync(Current, CancellationToken.None);
            await _saving;
        }
        catch (OperationCanceledException)
        {
            // A newer save request or a flush superseded this one.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(ex, "Settings could not be saved");
        }
        finally
        {
            pending.Dispose();
        }
    }
}

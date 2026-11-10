using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Repository.SettingsRepo;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Service.SettingsServ;

public sealed class SettingsService : ISettingsService
{
    private static readonly TimeSpan _saveDelay = TimeSpan.FromMilliseconds(750);

    public AppSettings Current { get; private set; } = new();

    private readonly ISettingsRepository _repository;
    private readonly ILogger<SettingsService> _logger;
    private CancellationTokenSource? _pendingSave;
    private bool _isLoaded;

    public SettingsService(ISettingsRepository repository, ILogger<SettingsService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Current = await _repository.LoadAsync(cancellationToken);
            _isLoaded = true;
            _logger.LogDebug("Settings loaded: schema={SchemaVersion}", Current.SchemaVersion);
        }
        catch (IOException ex)
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

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        _pendingSave?.Cancel();
        _pendingSave = null;
        return _isLoaded ? _repository.SaveAsync(Current, cancellationToken) : Task.CompletedTask;
    }

    private async Task SaveAfterDelayAsync(CancellationTokenSource pending)
    {
        try
        {
            await Task.Delay(_saveDelay, pending.Token);
            if (_pendingSave == pending)
                _pendingSave = null;

            await _repository.SaveAsync(Current, CancellationToken.None);
        }
        catch (OperationCanceledException)
        {
            // A newer save request or a flush superseded this one.
        }
        catch (IOException ex)
        {
            _logger.LogWarning(ex, "Settings could not be saved");
        }
        finally
        {
            pending.Dispose();
        }
    }
}

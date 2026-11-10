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

    public SettingsService(ISettingsRepository repository, ILogger<SettingsService> logger)
    {
        _repository = repository;
        _logger = logger;
    }

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        Current = await _repository.LoadAsync(cancellationToken);
        _logger.LogDebug("Settings loaded: schema={SchemaVersion}", Current.SchemaVersion);
    }

    public void RequestSave()
    {
        _pendingSave?.Cancel();
        var pending = new CancellationTokenSource();
        _pendingSave = pending;
        _ = SaveAfterDelayAsync(pending);
    }

    public Task FlushAsync(CancellationToken cancellationToken = default)
    {
        _pendingSave?.Cancel();
        _pendingSave = null;
        return _repository.SaveAsync(Current, cancellationToken);
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

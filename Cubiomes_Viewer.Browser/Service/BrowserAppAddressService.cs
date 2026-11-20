using System.Runtime.InteropServices.JavaScript;

using Cubiomes_Viewer.Browser.Interop;
using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Service.AppAddressServ;
using Cubiomes_Viewer.Service.MapLinkServ;
using Cubiomes_Viewer.Service.ViewHistoryServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Browser.Service;

/// <summary>
/// The page's address and history: links start with the address, the address bar follows the view, and each jump of the map adds
/// an entry to the browser's history, so that the browser's Back and Forward step through the jumps as the app's own buttons do.
/// Browsers limit how often a page may change its address, so a view is shown only once it has rested for a moment.
/// </summary>
internal sealed class BrowserAppAddressService : IAppAddressService, IViewHistoryService
{
    private static readonly TimeSpan _restDelay = TimeSpan.FromMilliseconds(500);

    private readonly IMapLinkService _mapLinks;
    private readonly ILogger<BrowserAppAddressService> _logger;
    private Task<JSObject>? _moduleImport;
    private CancellationTokenSource? _pending;
    private string? _shown;
    private string? _latest;
    private MapLink? _view;
    private bool _isWatching;
    private int _step;
    private int _lastStep;

    public BrowserAppAddressService(string? startAddress, IMapLinkService mapLinks, ILogger<BrowserAppAddressService> logger)
    {
        StartAddress = startAddress;
        LinkBase = Uri.TryCreate(startAddress, UriKind.Absolute, out var address) ? address.GetLeftPart(UriPartial.Path) : Constants.WebAppUrl;
        _shown = startAddress;
        _latest = startAddress;
        _mapLinks = mapLinks;
        _logger = logger;
    }

    public string? StartAddress { get; }
    public string LinkBase { get; }
    public bool HasAddressBar => true;
    public bool CanGoBack => _step > 0;
    public bool CanGoForward => _step < _lastStep;

    public event Action? Changed;

    public event Action<MapLink>? Moved;

    public void ShowAddress(string address)
    {
        ArgumentNullException.ThrowIfNull(address);

        _latest = address;
        _pending?.Cancel();
        _pending?.Dispose();
        _pending = new CancellationTokenSource();
        _ = ShowAfterRestAsync(address, _pending.Token);
    }

    /// <summary>
    /// Shows the view being left in the current entry at once and adds a new entry after it, which the views of the jump then
    /// replace as they are shown.
    /// </summary>
    public void Leave(MapLink view)
    {
        ArgumentNullException.ThrowIfNull(view);

        _pending?.Cancel();
        _step++;
        _lastStep = _step;
        Changed?.Invoke();
        _ = AddEntryAsync(_latest ?? LinkBase, _step);
    }

    public void Show(MapLink view)
    {
        ArgumentNullException.ThrowIfNull(view);

        _view = view;
        if (!_isWatching)
            _ = WatchHistoryAsync();
    }

    public void GoBack() => _ = CallPageAsync(PageInterop.GoBack);

    public void GoForward() => _ = CallPageAsync(PageInterop.GoForward);

    private async Task ShowAfterRestAsync(string address, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(_restDelay, cancellationToken);
            if (address == _shown)
                return;

            await ImportModuleAsync();
            PageInterop.ReplaceAddress(address);
            _shown = address;
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // A newer view replaced this one before it rested.
        }
        catch (JSException exception)
        {
            _logger.LogWarning(exception, "The address bar could not show {Address}", address);
        }
    }

    private async Task AddEntryAsync(string leaving, int step)
    {
        try
        {
            await ImportModuleAsync();
            PageInterop.ReplaceAddress(leaving);
            PageInterop.PushAddress(leaving, step);
            _shown = leaving;
        }
        catch (JSException exception)
        {
            _logger.LogWarning(exception, "The browser's history could not take the step from {Address}", leaving);
        }
    }

    /// <summary>Learns which step of the history the page opened on and starts following the browser's Back and Forward.</summary>
    private async Task WatchHistoryAsync()
    {
        _isWatching = true;
        try
        {
            await ImportModuleAsync();
            _step = _lastStep = PageInterop.HistoryStep();
            PageInterop.WatchHistory(OnHistoryMoved);
            Changed?.Invoke();
        }
        catch (JSException exception)
        {
            _isWatching = false;
            _logger.LogWarning(exception, "The browser's history could not be followed");
        }
    }

    /// <summary>Opens the view of the entry the browser moved to; an address that is no view of the map is left as it is.</summary>
    private void OnHistoryMoved(string address, int step)
    {
        _pending?.Cancel();
        _step = step;
        _shown = _latest = address;
        Changed?.Invoke();
        if (_view is { } view && _mapLinks.TryParse(address, view, out var link, out _))
            Moved?.Invoke(link);
    }

    private async Task CallPageAsync(Action call)
    {
        try
        {
            await ImportModuleAsync();
            call();
        }
        catch (JSException exception)
        {
            _logger.LogWarning(exception, "The browser's history could not move");
        }
    }

    /// <summary>Imports the page module once; a failed import is tried again by the next call.</summary>
    private async Task ImportModuleAsync()
    {
        _moduleImport ??= JSHost.ImportAsync(PageInterop.ModuleName, PageInterop.ModuleUrl);
        try
        {
            await _moduleImport;
        }
        catch (JSException)
        {
            _moduleImport = null;
            throw;
        }
    }
}

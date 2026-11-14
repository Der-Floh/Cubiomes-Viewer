using System.Runtime.InteropServices.JavaScript;

using Cubiomes_Viewer.Browser.Interop;
using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Service.AppAddressServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Browser.Service;

/// <summary>
/// The page's address: links start with it, and the address bar follows the view. Browsers limit how often a page may change its
/// address, so a new one is shown only once the view has rested for a moment.
/// </summary>
internal sealed class BrowserAppAddressService : IAppAddressService
{
    private static readonly TimeSpan _restDelay = TimeSpan.FromMilliseconds(500);

    private readonly ILogger<BrowserAppAddressService> _logger;
    private Task<JSObject>? _moduleImport;
    private CancellationTokenSource? _pending;
    private string? _shown;

    public BrowserAppAddressService(string? startAddress, ILogger<BrowserAppAddressService> logger)
    {
        StartAddress = startAddress;
        LinkBase = Uri.TryCreate(startAddress, UriKind.Absolute, out var address) ? address.GetLeftPart(UriPartial.Path) : Constants.WebAppUrl;
        _shown = startAddress;
        _logger = logger;
    }

    public string? StartAddress { get; }
    public string LinkBase { get; }
    public bool HasAddressBar => true;

    public void ShowAddress(string address)
    {
        ArgumentNullException.ThrowIfNull(address);

        _pending?.Cancel();
        _pending?.Dispose();
        _pending = new CancellationTokenSource();
        _ = ShowAfterRestAsync(address, _pending.Token);
    }

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

using Cubiomes_Viewer.Data;

namespace Cubiomes_Viewer.Service.AppAddressServ;

/// <summary>The app without a web address of its own, as on the desktop: it opens no link at start, and its links point at the web version.</summary>
public sealed class NoAddressBarService : IAppAddressService
{
    public string? StartAddress => null;
    public string LinkBase => Constants.WebAppUrl;
    public bool HasAddressBar => false;

    public void ShowAddress(string address) { }
}

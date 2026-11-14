using Cubiomes_Viewer.Service.AppAddressServ;

namespace Cubiomes_Viewer.Tests;

/// <summary>An address bar for tests: the address the app opens with can be set, and every address it shows is kept.</summary>
internal sealed class RecordingAppAddressService : IAppAddressService
{
    public const string TestLinkBase = "https://example.test/viewer/";

    public string? StartAddress { get; set; }
    public string LinkBase => TestLinkBase;
    public bool HasAddressBar => true;
    public List<string> Shown { get; } = [];

    public void ShowAddress(string address) => Shown.Add(address);
}

namespace Cubiomes_Viewer.Service.AppAddressServ;

/// <summary>The web address of the app where it has one: the address it was opened with, and an address bar that can show the view.</summary>
public interface IAppAddressService
{
    /// <summary>The address the app was opened with, or <see langword="null"/> where it has none, as on the desktop.</summary>
    string? StartAddress { get; }

    /// <summary>What links to a view start with: the page's own address in the browser, the web version's elsewhere.</summary>
    string LinkBase { get; }

    /// <summary>Whether there is an address bar that <see cref="ShowAddress"/> changes.</summary>
    bool HasAddressBar { get; }

    /// <summary>Shows an address in the address bar without adding to the browser's history; does nothing without an address bar.</summary>
    void ShowAddress(string address);
}

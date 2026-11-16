namespace Cubiomes_Viewer.Service.UpdateServ;

/// <summary>How a copy of the app gets from a newer release to running it.</summary>
public enum UpdateStyle
{
    /// <summary>A newer release downloads in the background and installs when the app closes or restarts, as on the desktop.</summary>
    Background,

    /// <summary>
    /// A newer release is offered, downloads once asked for and installs once confirmed in the system's installer, as on
    /// Android, where the download may come over mobile data and every installation needs the user's go-ahead.
    /// </summary>
    OnRequest,
}

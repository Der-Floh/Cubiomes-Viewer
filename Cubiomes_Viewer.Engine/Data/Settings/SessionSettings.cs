namespace Cubiomes_Viewer.Engine.Data.Settings;

/// <summary>What the app takes up again when it starts.</summary>
public sealed class SessionSettings
{
    /// <summary>Whether the seed, the place on the map and the search of last time are opened again.</summary>
    public bool RestoreLastSession { get; set; } = true;

    /// <summary>Whether the main window opens where and as large as it was closed, on the desktop.</summary>
    public bool RememberWindow { get; set; } = true;

    public WindowPlacement? Window { get; set; }
}

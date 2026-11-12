namespace Cubiomes_Viewer.Service.WindowServ;

/// <summary>What the platform offers in windows: on the desktop the app has windows of its own, elsewhere one view.</summary>
public interface IWindowService
{
    /// <summary>Whether the app runs in windows it can place and open more of.</summary>
    bool HasWindows { get; }
}

namespace Cubiomes_Viewer.Service.ProgressServ;

/// <summary>
/// How far the actions that measure their progress have come, for the status bar: while several run, the one that began last
/// and has reported shows.
/// </summary>
public interface IProgressService
{
    /// <summary>The share of the action shown, from 0 to 1, or <see langword="null"/> while none shows.</summary>
    double? Share { get; }

    /// <summary>Raised on the thread of the change whenever the share shown changes.</summary>
    event EventHandler? Changed;

    /// <summary>Starts an action's part in the progress; it shows from its first report until it is disposed.</summary>
    IProgressScope Begin();
}

namespace Cubiomes_Viewer.Engine.Data.Search;

public enum SearchState
{
    /// <summary>No search was started, or the last one was reset.</summary>
    Idle,

    Running,

    /// <summary>Stopped by the user or by a failure; it can continue where it stopped.</summary>
    Paused,

    /// <summary>Every seed was scanned or enough seeds were found.</summary>
    Finished,
}

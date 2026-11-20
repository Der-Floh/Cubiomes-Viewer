using Cubiomes_Viewer.Data;

namespace Cubiomes_Viewer.Service.ViewHistoryServ;

/// <summary>
/// The views the map jumped between, such as to a found seed or a line of an analysis, so that Back and Forward step through
/// them. Moving the map by hand stays within a step. In the browser the page's own history holds the steps.
/// </summary>
public interface IViewHistoryService
{
    bool CanGoBack { get; }

    bool CanGoForward { get; }

    /// <summary>Raised when <see cref="CanGoBack"/> or <see cref="CanGoForward"/> may have changed.</summary>
    event Action? Changed;

    /// <summary>Raised with the view to open after the history moved to another step.</summary>
    event Action<MapLink>? Moved;

    /// <summary>Keeps <paramref name="view"/> as the step a jump leaves, drops the steps ahead of it, and makes the next view shown a new step.</summary>
    void Leave(MapLink view);

    /// <summary>Notes the view the map shows now, as the latest of the current step.</summary>
    void Show(MapLink view);

    void GoBack();

    void GoForward();
}

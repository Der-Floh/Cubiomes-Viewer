namespace Cubiomes_Viewer.Service.UiZoomServ;

/// <summary>How much larger than normal the whole interface is drawn.</summary>
public interface IUiZoomService
{
    /// <summary>Raised on the UI thread when <see cref="Scale"/> changed.</summary>
    event EventHandler? Changed;

    /// <summary>The factor the interface is scaled by: 1 at 100 %.</summary>
    double Scale { get; }

    void Apply(int percent);
}

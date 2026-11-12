namespace Cubiomes_Viewer.ViewModels;

/// <summary>A side panel that prepares itself each time it is opened, for instance from what the map shows then.</summary>
public interface IOpenablePanel
{
    void OnOpened();
}

using Avalonia.Controls;

namespace Cubiomes_Viewer.Service.TopLevelServ;

public interface ITopLevelProvider
{
    void SetTopLevel(TopLevel topLevel);
    TopLevel GetTopLevel();
}

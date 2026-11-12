using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;

namespace Cubiomes_Viewer.Service.WindowServ;

public sealed class WindowService : IWindowService
{
    public bool HasWindows => Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime;
}

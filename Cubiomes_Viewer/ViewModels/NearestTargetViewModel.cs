using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>An entry of the Find nearest menus: its target, the marker the map draws it with, and the command that searches for it.</summary>
public sealed class NearestTargetViewModel
{
    public NearestTarget Target { get; }
    public string Name => Target.Name;

    /// <summary>The badge of the target: a structure's or the strongholds' marker, or a biome's badge.</summary>
    public MapMarker Marker { get; }

    public IAsyncRelayCommand FindCommand { get; }

    public NearestTargetViewModel(NearestTarget target, MapMarker marker, Func<NearestTarget, Task> find)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(marker);
        ArgumentNullException.ThrowIfNull(find);

        Target = target;
        Marker = marker;
        FindCommand = new AsyncRelayCommand(() => find(target), AsyncRelayCommandOptions.AllowConcurrentExecutions);
    }
}

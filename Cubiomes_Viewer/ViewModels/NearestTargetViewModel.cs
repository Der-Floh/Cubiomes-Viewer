using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>An entry of the Find nearest menus: its target, the marker the map draws it with, and the command that searches for it.</summary>
public sealed class NearestTargetViewModel
{
    public NearestTarget Target { get; }
    public string Name => Target.Name;

    /// <summary>
    /// The badge of the target: a structure's or the strongholds' marker, or a biome's badge. It is made when the menu
    /// first shows it, as making the badges of every target takes long and the menus are rarely opened.
    /// </summary>
    public MapMarker Marker => field ??= _markerOf(Target);

    public IAsyncRelayCommand FindCommand { get; }

    private readonly Func<NearestTarget, MapMarker> _markerOf;

    public NearestTargetViewModel(NearestTarget target, Func<NearestTarget, MapMarker> markerOf, Func<NearestTarget, Task> find)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(markerOf);
        ArgumentNullException.ThrowIfNull(find);

        Target = target;
        _markerOf = markerOf;
        FindCommand = new AsyncRelayCommand(() => find(target), AsyncRelayCommandOptions.AllowConcurrentExecutions);
    }
}

using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>A pin in the list of the Layers panel: choosing it moves the map there and picks the pin.</summary>
public sealed partial class PinEntryViewModel
{
    public MapPin Pin { get; }
    public string Name { get; }
    public string Place { get; }
    public PinLook Look { get; }

    private readonly Action<MapPin> _open;

    public PinEntryViewModel(MapPin pin, PinLook look, Action<MapPin> open)
    {
        ArgumentNullException.ThrowIfNull(pin);
        ArgumentNullException.ThrowIfNull(look);
        ArgumentNullException.ThrowIfNull(open);

        Pin = pin;
        Name = pin.Name ?? MapSelection.PinName;
        Place = BlockPosText.ToBlock(new BlockPos(pin.X, pin.Z));
        Look = look;
        _open = open;
    }

    [RelayCommand]
    public void Open() => _open(Pin);
}

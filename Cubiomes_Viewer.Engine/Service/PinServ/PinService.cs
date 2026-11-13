using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SettingsServ;

namespace Cubiomes_Viewer.Engine.Service.PinServ;

public sealed class PinService : IPinService
{
    public event Action? Changed;

    private readonly ISettingsService _settingsService;

    private List<MapPin> Pins => _settingsService.Current.Map.Pins;

    public PinService(ISettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    public IReadOnlyList<MapPin> GetPins(long seed, Dimension dimension)
        => [.. Pins.Where(pin => pin.Seed == seed && pin.Dimension == dimension)];

    public MapPin Add(long seed, Dimension dimension, BlockPos position)
    {
        var pin = new MapPin { Seed = seed, Dimension = dimension, X = position.X, Z = position.Z };
        Pins.Add(pin);
        StoreChange();
        return pin;
    }

    public void SetIcon(MapPin pin, string? icon)
    {
        ArgumentNullException.ThrowIfNull(pin);

        if (string.Equals(pin.Icon, icon, StringComparison.Ordinal))
            return;

        pin.Icon = icon;
        StoreChange();
    }

    public void Remove(MapPin pin)
    {
        ArgumentNullException.ThrowIfNull(pin);

        if (Pins.Remove(pin))
            StoreChange();
    }

    private void StoreChange()
    {
        _settingsService.RequestSave();
        Changed?.Invoke();
    }
}

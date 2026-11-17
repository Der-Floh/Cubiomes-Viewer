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

    public IReadOnlyList<MapPin> AddCopies(IEnumerable<MapPin> pins)
    {
        ArgumentNullException.ThrowIfNull(pins);

        var taken = Pins.Select(GetSpot).ToHashSet();
        List<MapPin> added = [];
        foreach (var pin in pins)
        {
            if (!taken.Add(GetSpot(pin)))
                continue;

            var copy = new MapPin { Seed = pin.Seed, Dimension = pin.Dimension, X = pin.X, Z = pin.Z, Name = ToName(pin.Name), Icon = pin.Icon };
            Pins.Add(copy);
            added.Add(copy);
        }

        if (added.Count > 0)
            StoreChange();
        return added;
    }

    public void Move(MapPin pin, BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(pin);

        if (pin.X == position.X && pin.Z == position.Z)
            return;

        (pin.X, pin.Z) = (position.X, position.Z);
        StoreChange();
    }

    public void Rename(MapPin pin, string? name)
    {
        ArgumentNullException.ThrowIfNull(pin);

        var trimmed = ToName(name);
        if (string.Equals(pin.Name, trimmed, StringComparison.Ordinal))
            return;

        pin.Name = trimmed;
        StoreChange();
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

    private static string? ToName(string? name) => string.IsNullOrWhiteSpace(name) ? null : name.Trim();

    private static (long Seed, Dimension Dimension, int X, int Z) GetSpot(MapPin pin) => (pin.Seed, pin.Dimension, pin.X, pin.Z);
}

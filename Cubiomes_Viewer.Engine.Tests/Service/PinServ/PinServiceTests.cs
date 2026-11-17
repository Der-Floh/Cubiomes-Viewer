using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.PinServ;
using Cubiomes_Viewer.Engine.Tests.Service.SettingsServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.PinServ;

public sealed class PinServiceTests
{
    private readonly FakeSettingsService _settings = new();
    private readonly PinService _pins;
    private int _changes;

    public PinServiceTests()
    {
        _pins = new PinService(_settings);
        _pins.Changed += () => _changes++;
    }

    [Fact]
    public void Add_APin_StoresItForItsSeedAndDimensionOnly()
    {
        var pin = _pins.Add(262, Dimension.Overworld, new BlockPos(-40, 96));
        _pins.Add(262, Dimension.Nether, new BlockPos(5, 5));
        _pins.Add(263, Dimension.Overworld, new BlockPos(5, 5));

        Assert.Equal([pin], _pins.GetPins(262, Dimension.Overworld));
        Assert.Equal((262L, Dimension.Overworld, -40, 96, (string?)null), (pin.Seed, pin.Dimension, pin.X, pin.Z, pin.Icon));
        Assert.Equal(3, _settings.Current.Map.Pins.Count);
        Assert.Equal((3, 3), (_changes, _settings.SaveRequests));
    }

    [Fact]
    public void AddCopies_SharedPins_AddsCopiesWithTheirNamesAndLooksAndTellsOnce()
    {
        MapPin[] shared =
        [
            new() { Seed = 262, Dimension = Dimension.Overworld, X = 10, Z = 20, Name = "  Home ", Icon = "Village" },
            new() { Seed = 262, Dimension = Dimension.Nether, X = 1, Z = 2 },
        ];

        var added = _pins.AddCopies(shared);

        Assert.Equal(2, added.Count);
        Assert.DoesNotContain(added, pin => shared.Contains(pin));
        Assert.Equal((262L, Dimension.Overworld, 10, 20, "Home", "Village"), (added[0].Seed, added[0].Dimension, added[0].X, added[0].Z, added[0].Name, added[0].Icon));
        Assert.Equal([added[1]], _pins.GetPins(262, Dimension.Nether));
        Assert.Equal((1, 1), (_changes, _settings.SaveRequests));
    }

    [Fact]
    public void AddCopies_PinOnABlockThatHoldsOneAlready_IsLeftOutAndTheOldOneKept()
    {
        var own = _pins.Add(262, Dimension.Overworld, new BlockPos(10, 20));
        _pins.Rename(own, "Base");

        var added = _pins.AddCopies(
        [
            new() { Seed = 262, Dimension = Dimension.Overworld, X = 10, Z = 20, Name = "Home" },
            new() { Seed = 262, Dimension = Dimension.Overworld, X = 30, Z = 40, Name = "Farm" },
            new() { Seed = 262, Dimension = Dimension.Overworld, X = 30, Z = 40, Name = "Farm again" },
        ]);

        Assert.Equal(["Farm"], added.Select(pin => pin.Name));
        Assert.Equal(["Base", "Farm"], _pins.GetPins(262, Dimension.Overworld).Select(pin => pin.Name));
    }

    [Fact]
    public void AddCopies_NothingNew_TellsNothing()
    {
        _pins.Add(262, Dimension.Overworld, new BlockPos(10, 20));
        _changes = 0;

        var added = _pins.AddCopies([new() { Seed = 262, Dimension = Dimension.Overworld, X = 10, Z = 20 }]);

        Assert.Empty(added);
        Assert.Equal(0, _changes);
    }

    [Fact]
    public void SetIcon_NewIcon_StoresItAndTellsOnce()
    {
        var pin = _pins.Add(262, Dimension.Overworld, new BlockPos(0, 0));

        _pins.SetIcon(pin, "Village");
        _pins.SetIcon(pin, "Village");

        Assert.Equal("Village", _settings.Current.Map.Pins.Single().Icon);
        Assert.Equal((2, 2), (_changes, _settings.SaveRequests));
    }

    [Fact]
    public void Move_ToAnotherBlock_StoresItAndTellsOnce()
    {
        var pin = _pins.Add(262, Dimension.Overworld, new BlockPos(0, 0));

        _pins.Move(pin, new BlockPos(16, -8));
        _pins.Move(pin, new BlockPos(16, -8));

        Assert.Equal((16, -8), (pin.X, pin.Z));
        Assert.Equal((2, 2), (_changes, _settings.SaveRequests));
    }

    [Fact]
    public void Rename_WithSpaces_KeepsTheTrimmedNameAndBlankTakesItAway()
    {
        var pin = _pins.Add(262, Dimension.Overworld, new BlockPos(0, 0));

        _pins.Rename(pin, "  Base camp ");
        Assert.Equal("Base camp", pin.Name);

        _pins.Rename(pin, "   ");
        Assert.Null(pin.Name);
        Assert.Equal(3, _changes);
    }

    [Fact]
    public void Remove_APin_TakesItAwayAndIgnoresOneThatIsGone()
    {
        var pin = _pins.Add(262, Dimension.Overworld, new BlockPos(0, 0));

        _pins.Remove(pin);
        _pins.Remove(pin);

        Assert.Empty(_pins.GetPins(262, Dimension.Overworld));
        Assert.Equal((2, 2), (_changes, _settings.SaveRequests));
    }
}

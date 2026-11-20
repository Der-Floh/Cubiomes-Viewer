using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Service.ViewHistoryServ;

namespace Cubiomes_Viewer.Tests.Service;

public sealed class ViewHistoryServiceTests
{
    private readonly ViewHistoryService _history = new();
    private readonly List<MapLink> _opened = [];

    public ViewHistoryServiceTests() => _history.Moved += _opened.Add;

    [Fact]
    public void Leave_ThenShow_MakesTheViewLeftAStepBehind()
    {
        _history.Show(At(0));
        var atStart = (_history.CanGoBack, _history.CanGoForward);

        _history.Leave(At(10));
        _history.Show(At(500));
        _history.GoBack();

        Assert.Equal((false, false), atStart);
        Assert.Equal([At(10)], _opened);
        Assert.Equal((false, true), (_history.CanGoBack, _history.CanGoForward));
    }

    [Fact]
    public void Show_WithoutAJump_KeepsTheStepAndItsLatestView()
    {
        _history.Show(At(0));
        _history.Leave(At(0));
        _history.Show(At(500));

        _history.Show(At(510));
        _history.Show(At(520));
        _history.GoBack();
        _history.GoForward();

        Assert.Equal([At(0), At(520)], _opened);
    }

    [Fact]
    public void Leave_AfterGoingBack_DropsTheStepsAhead()
    {
        _history.Show(At(0));
        _history.Leave(At(0));
        _history.Show(At(500));
        _history.GoBack();

        _history.Leave(At(5));
        _history.Show(At(900));

        Assert.False(_history.CanGoForward);
        _history.GoBack();
        Assert.Equal(At(5), _opened[^1]);
        Assert.False(_history.CanGoBack);
    }

    [Fact]
    public void GoBackAndForward_AtTheEnds_DoNothing()
    {
        _history.GoBack();
        _history.Show(At(0));
        _history.GoBack();
        _history.GoForward();

        Assert.Empty(_opened);
    }

    [Fact]
    public void Changed_IsRaisedWhenTheStepsChange()
    {
        var changes = 0;
        _history.Changed += () => changes++;
        _history.Show(At(0));

        _history.Leave(At(0));
        _history.Show(At(500));
        _history.Show(At(510));
        _history.GoBack();

        Assert.Equal(4, changes);
    }

    private static MapLink At(int x) => new(262, McVersion.V1_21_3, Dimension.Overworld, LargeBiomes: false, new BlockPos(x, 0), 4, LayerMode.Biomes, 256, 0);
}

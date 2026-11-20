using Cubiomes_Viewer.Service.ProgressServ;

namespace Cubiomes_Viewer.Tests.Service;

public sealed class ProgressServiceTests
{
    private readonly ProgressService _service = new();
    private int _changes;

    public ProgressServiceTests() => _service.Changed += (_, _) => _changes++;

    [Fact]
    public void Begin_WithoutAReport_ShowsNothing()
    {
        using var scope = _service.Begin();

        Assert.Null(_service.Share);
        Assert.Equal(0, _changes);
    }

    [Fact]
    public void Report_OfOneAction_ShowsItsShareUntilItEnds()
    {
        var scope = _service.Begin();

        scope.Report(0.4);
        var shown = _service.Share;
        scope.Dispose();

        Assert.Equal(0.4, shown);
        Assert.Null(_service.Share);
        Assert.Equal(2, _changes);
    }

    [Fact]
    public void Report_OfANewerAction_ShowsItAndTheOlderOneComesBackWhenItEnds()
    {
        using var older = _service.Begin();
        older.Report(0.2);
        var newer = _service.Begin();
        newer.Report(0.7);
        older.Report(0.3);
        var whileBoth = (_service.Share, _changes);

        newer.Dispose();

        Assert.Equal((0.7, 2), whileBoth);
        Assert.Equal(0.3, _service.Share);
    }

    [Fact]
    public void Begin_OfANewerActionThatNeverReports_KeepsTheOlderOneShown()
    {
        using var older = _service.Begin();
        older.Report(0.5);

        using var silent = _service.Begin();

        Assert.Equal(0.5, _service.Share);
    }

    [Fact]
    public void Dispose_OfTheOlderAction_KeepsTheNewerOneShown()
    {
        var older = _service.Begin();
        older.Report(0.2);
        using var newer = _service.Begin();
        newer.Report(0.6);
        var changes = _changes;

        older.Dispose();

        Assert.Equal((0.6, changes), (_service.Share, _changes));
    }

    [Fact]
    public void Dispose_Twice_EndsTheActionOnce()
    {
        using var other = _service.Begin();
        other.Report(0.1);
        var scope = _service.Begin();
        scope.Report(0.9);

        scope.Dispose();
        scope.Dispose();

        Assert.Equal(0.1, _service.Share);
    }

    [Fact]
    public void Report_AfterTheActionEnded_IsDropped()
    {
        var scope = _service.Begin();
        scope.Dispose();

        scope.Report(0.5);

        Assert.Null(_service.Share);
        Assert.Equal(0, _changes);
    }

    [Theory]
    [InlineData(-0.5, 0)]
    [InlineData(1.5, 1)]
    public void Report_OutsideTheWhole_IsClamped(double share, double expected)
    {
        using var scope = _service.Begin();

        scope.Report(share);

        Assert.Equal(expected, _service.Share);
    }

    [Fact]
    public void Report_NotANumber_IsIgnored()
    {
        using var scope = _service.Begin();
        scope.Report(0.25);

        scope.Report(double.NaN);

        Assert.Equal((0.25, 1), (_service.Share, _changes));
    }

    [Fact]
    public void Report_OfTheShareShownAlready_TellsNothing()
    {
        using var scope = _service.Begin();
        scope.Report(0.5);

        scope.Report(0.5);

        Assert.Equal(1, _changes);
    }

    [Fact]
    public async Task Reports_FromManyThreads_EndWithNothingShown()
    {
        await Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
        {
            using var scope = _service.Begin();
            for (var step = 0; step <= 100; step++)
                scope.Report(step / 100.0);
        })));

        Assert.Null(_service.Share);
    }
}

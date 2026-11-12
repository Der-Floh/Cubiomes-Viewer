using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Service.SearchServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.SearchServ;

public sealed class SeedSequenceTests
{
    private const long UpperStride = 1L << 48;
    private static readonly SearchDefinition _definition = new();

    [Fact]
    public void Range_FromFirstToLast_CountsBothAndBatchesStopAtTheEnd()
    {
        var sequence = new SeedRangeSequence(new SeedRangeSource { FirstSeed = -5, LastSeed = 4 });

        var batch = sequence.CreateBatch(_definition, 7, 100);

        Assert.Equal((UInt128)10, sequence.Count);
        Assert.False(sequence.IsEndless);
        Assert.Equal(-5, sequence.GetSeed(0));
        Assert.Equal((2, 3), (batch.FirstSeed, batch.Count));
        Assert.Equal(4, batch.GetSeed(2));
    }

    [Fact]
    public void Range_EverySeed_HasTwoToTheSixtyFourSeedsAndNoEnd()
    {
        var sequence = new SeedRangeSequence(new SeedRangeSource { FirstSeed = long.MinValue });

        Assert.Equal(UInt128.One << 64, sequence.Count);
        Assert.True(sequence.IsEndless);
        Assert.Equal(long.MaxValue, sequence.GetSeed(ulong.MaxValue));
    }

    [Fact]
    public void QuadHuts_EachLowerPart_IsFollowedByEveryUpperPartInBatchesThatStayWithIt()
    {
        var sequence = new QuadHutSeedSequence([0x1234, 0x5678]);

        var batch = sequence.CreateBatch(_definition, 65_530, 100);

        Assert.Equal((UInt128)(2 * 65_536), sequence.Count);
        Assert.Equal(0x1234 | (3L << 48), sequence.GetSeed(3));
        Assert.Equal(0x5678, sequence.GetSeed(65_536));
        Assert.Equal((6, UpperStride), (batch.Count, batch.Stride));
        Assert.Equal(sequence.GetSeed(65_535), batch.GetSeed(5));
        Assert.Equal(unchecked(0x1234 | (65_535L << 48)), sequence.GetSeed(65_535));
    }

    [Fact]
    public void List_Batches_CarryTheirSeedsInTheOrderOfTheList()
    {
        var sequence = new SeedListSequence(new SeedListSource { Seeds = [9, -3, 9, 7, 100] });

        var batch = sequence.CreateBatch(_definition, 1, 3);
        var last = sequence.CreateBatch(_definition, 4, 3);

        Assert.Equal((UInt128)5, sequence.Count);
        Assert.Equal([-3L, 9, 7], Assert.IsType<long[]>(batch.Seeds));
        Assert.Equal(-3, batch.GetSeed(0));
        Assert.Equal([100L], Assert.IsType<long[]>(last.Seeds));
    }
}

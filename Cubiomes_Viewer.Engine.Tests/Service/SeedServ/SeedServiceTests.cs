using Cubiomes_Viewer.Engine.Data.Seeds;
using Cubiomes_Viewer.Engine.Service.SeedServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.SeedServ;

public sealed class SeedServiceTests
{
    private readonly SeedParser _parser = new();

    [Theory]
    [InlineData("0", 0L)]
    [InlineData("  262 ", 262L)]
    [InlineData("-9223372036854775808", long.MinValue)]
    [InlineData("+17", 17L)]
    public void Parse_Number_IsUsedAsSeed(string text, long expected)
        => Assert.Equal(new SeedParseResult(expected, SeedKind.Numeric), _parser.Parse(text));

    [Theory]
    [InlineData("hello", 99162322L)]
    [InlineData("abc", 96354L)]
    [InlineData("polygenelubricants", int.MinValue)]
    public void Parse_Text_IsHashedLikeAJavaString(string text, long expected)
        => Assert.Equal(new SeedParseResult(expected, SeedKind.Text), _parser.Parse(text));

    [Fact]
    public void Parse_NumberOutsideLongRange_IsTreatedAsText()
    {
        var result = _parser.Parse("9223372036854775808");

        Assert.Equal(SeedKind.Text, result.Kind);
        Assert.InRange(result.Seed, int.MinValue, int.MaxValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Parse_EmptyInput_PicksRandomSeed(string? text)
        => Assert.Equal(SeedKind.Random, _parser.Parse(text).Kind);

    [Fact]
    public void Add_RepeatedAndNewSeeds_KeepsNewestFirstWithoutDuplicates()
    {
        var history = new SeedHistoryService();

        history.Add(1);
        history.Add(2);
        history.Add(1);

        Assert.Equal([1, 2], history.Seeds);
    }

    [Fact]
    public void Add_MoreThanCapacity_DropsOldestSeeds()
    {
        var history = new SeedHistoryService();

        for (var seed = 0; seed < SeedHistoryService.Capacity + 4; seed++)
            history.Add(seed);

        Assert.Equal(SeedHistoryService.Capacity, history.Seeds.Count);
        Assert.Equal(SeedHistoryService.Capacity + 3, history.Seeds[0]);
    }

    [Fact]
    public void Load_StoredSeeds_ReplacesHistoryAndRaisesChanged()
    {
        var history = new SeedHistoryService();
        history.Add(99);
        var changes = 0;
        history.Changed += () => changes++;

        history.Load([5, 6, 5]);

        Assert.Equal([5, 6], history.Seeds);
        Assert.Equal(1, changes);
    }
}

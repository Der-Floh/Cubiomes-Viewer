using System.Text.Json;

using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Repository.SessionRepo;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

/// <summary>Conditions limited by the number of findings, which look outward nearest first instead of inside an area.</summary>
public sealed class FindingsLimitTests : IDisposable
{
    private const McVersion Version = McVersion.V1_21_3;
    private const int WideSide = 24_000;

    private static readonly LootRequirement _diamonds = new() { Item = "minecraft:diamond" };

    private readonly ConditionTestBench _bench = new();

    public void Dispose() => _bench.Dispose();

    [Theory]
    [InlineData(1L, 1)]
    [InlineData(262L, 3)]
    [InlineData(-5L, 5)]
    public void Structure_Findings_AreTheNearestTheAreaSearchFinds(long seed, int count)
    {
        var condition = Village(count);

        var (status, matches) = _bench.Evaluate(condition, Version, seed, SearchPass.Full);

        Assert.Equal(MatchStatus.Matched, status);
        Assert.Equal(NearestInWideArea(condition, seed, ConditionTestBench.Origin, count), matches);
    }

    [Fact]
    public void Structure_FindingsAroundAnotherPoint_AreTheNearestToIt()
    {
        var reference = new BlockPos(5_000, -3_000);
        var condition = Village(2);

        var (_, matches) = _bench.Evaluate(condition, Version, 262, SearchPass.Full, reference);

        Assert.Equal(NearestInWideArea(condition, 262, reference, 2), matches);
    }

    [Fact]
    public void Structure_FindingsWithLoot_AreTheNearestWhoseChestsHoldTheItem()
    {
        var condition = new StructureCondition { Id = 1, Type = StructureType.DesertPyramid, Limit = SearchLimit.Findings, Loot = _diamonds };

        var (status, matches) = _bench.Evaluate(condition, Version, 262, SearchPass.Full);

        Assert.Equal(MatchStatus.Matched, status);
        Assert.Equal(NearestInWideArea(condition, 262, ConditionTestBench.Origin, 1), matches);
    }

    [Fact]
    public void Structure_FindingsInTheQuickPass_AreLeftUndecided()
    {
        var (status, candidates) = _bench.Evaluate(Village(1), Version, 262, SearchPass.Quick);

        Assert.Equal(MatchStatus.Unknown, status);
        Assert.Empty(candidates);
    }

    [Fact]
    public void Structure_FindingsOfAStoppedJob_StopTheSearch()
    {
        using var stopping = new CancellationTokenSource();
        var condition = Village(1);
        var context = _bench.OpenStoppedBy(ConditionTestBench.Define(Version, condition), 262, stopping.Token);
        stopping.Cancel();

        Assert.ThrowsAny<OperationCanceledException>(() => _bench.Registry.GetEvaluator(condition).Evaluate(condition, ConditionTestBench.Origin, context, SearchPass.Full, []));
    }

    [Theory]
    [InlineData(1)]
    [InlineData(4)]
    public void Stronghold_Findings_AreTheStrongholdsNearestToThePoint(int count)
    {
        var reference = new BlockPos(-2_000, 7_500);
        var expected = _bench.GetGenerator(Version, 262).FindStrongholds().OrderBy(position => position.SquaredDistanceTo(reference)).Take(count).ToList();

        var (status, matches) = _bench.Evaluate(new StrongholdCondition { Id = 1, Limit = SearchLimit.Findings, Count = count }, Version, 262, SearchPass.Full, reference);

        Assert.Equal(MatchStatus.Matched, status);
        Assert.Equal(expected, matches);
    }

    [Fact]
    public void Stronghold_FindingsWithLoot_IsTheNearestWhoseChestsHoldTheItem()
    {
        var requirement = new LootRequirement { Item = "minecraft:enchanted_book", Enchantment = new LootEnchantment { Id = "minecraft:unbreaking", MinLevel = 2 } };
        var generator = _bench.GetGenerator(Version, 262);
        var expected = generator.FindStrongholds()
            .OrderBy(position => position.SquaredDistanceTo(ConditionTestBench.Origin))
            .First(position => generator.CountLoot(StructureType.Stronghold, position, requirement.ToQuery()) is { } loot && requirement.IsMetBy(loot));

        var (_, matches) = _bench.Evaluate(new StrongholdCondition { Id = 1, Limit = SearchLimit.Findings, Loot = requirement }, Version, 262, SearchPass.Full);

        Assert.Equal([expected], matches);
    }

    [Fact]
    public void Validate_FindingsOfAnExclusion_IsAProblem()
    {
        var problems = TestEngine.CreateConditionValidator().Validate(ConditionTestBench.Define(Version, Village(1) with { Count = 0 }));

        Assert.Equal("A condition that looks until it finds something cannot ask for none.", Assert.Single(problems).Message);
    }

    [Fact]
    public void Validate_FindingsOfAKindThatOnlyLooksInAnArea_IsAProblem()
    {
        var condition = new SlimeChunkCondition { Id = 1, Limit = SearchLimit.Findings, Count = 3 };

        var problems = TestEngine.CreateConditionValidator().Validate(ConditionTestBench.Define(Version, condition));

        Assert.Equal("This kind of condition looks only inside an area.", Assert.Single(problems).Message);
    }

    [Fact]
    public void Validate_Findings_LeaveTheAreaAlone()
    {
        var condition = Village(1) with { Area = SearchArea.Square(2 * SearchArea.MaxOffset + 2) };

        Assert.Empty(TestEngine.CreateConditionValidator().Validate(ConditionTestBench.Define(Version, condition)));
    }

    [Fact]
    public void Session_ConditionLimitedByFindings_IsStoredWithItsLimitAndReadBack()
    {
        var session = new SearchSession { Setup = new SearchSetup { Definition = ConditionTestBench.Define(Version, Village(3), Village(1) with { Id = 2, Limit = SearchLimit.Area }) } };

        var json = JsonSerializer.Serialize(session, SessionJsonContext.Default.SearchSession);
        var read = JsonSerializer.Deserialize(json, SessionJsonContext.Default.SearchSession);

        Assert.Equal(1, json.Split("\"limit\"").Length - 1);
        Assert.Contains("\"limit\":\"Findings\"", json, StringComparison.Ordinal);
        Assert.Equal([SearchLimit.Findings, SearchLimit.Area], read?.Setup.Definition.Conditions.Select(condition => condition.Limit));
    }

    private static StructureCondition Village(int count) => new() { Id = 1, Type = StructureType.Village, Limit = SearchLimit.Findings, Count = count };

    /// <summary>The matches an area search finds far around the point, nearest first, as many as asked for.</summary>
    private List<BlockPos> NearestInWideArea(StructureCondition condition, long seed, BlockPos reference, int count)
    {
        var (_, matches) = _bench.Evaluate(condition with { Limit = SearchLimit.Area, Area = SearchArea.Square(WideSide), Count = 1 }, Version, seed, SearchPass.Full, reference);
        var nearest = matches.OrderBy(position => position.SquaredDistanceTo(reference)).ThenBy(position => position.Z).ThenBy(position => position.X).Take(count).ToList();
        Assert.True(nearest.Count == count && Math.Sqrt(nearest[^1].SquaredDistanceTo(reference)) < WideSide / 2.0, "The wide area does not hold the nearest matches for sure.");
        return nearest;
    }
}

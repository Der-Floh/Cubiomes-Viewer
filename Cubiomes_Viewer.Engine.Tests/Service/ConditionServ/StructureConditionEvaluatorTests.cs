using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;

namespace Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

public sealed class StructureConditionEvaluatorTests : IDisposable
{
    private const McVersion Version = McVersion.V1_16_5;
    private const int SeedCount = 24;
    private const int Side = 2048;

    private readonly ConditionTestBench _bench = new();

    public void Dispose() => _bench.Dispose();

    public static TheoryData<long> Seeds() => [.. Enumerable.Range(0, SeedCount).Select(index => 1000L + index * 7919)];

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Evaluate_FullPass_FindsTheStructuresTheGeneratorFindsInTheArea(long seed)
    {
        var condition = Village(SearchArea.Square(Side));
        var expected = FindVillages(seed, condition.Area, ConditionTestBench.Origin);

        var (status, matches) = _bench.Evaluate(condition, Version, seed, SearchPass.Full);

        Assert.Equal(expected.Count > 0 ? MatchStatus.Matched : MatchStatus.Failed, status);
        Assert.Equal(expected, matches);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Evaluate_QuickPass_ListsCandidatesThatIncludeEveryStructure(long seed)
    {
        var condition = Village(SearchArea.Square(Side));

        var (status, candidates) = _bench.Evaluate(condition, Version, seed, SearchPass.Quick);
        var (_, matches) = _bench.Evaluate(condition, Version, seed, SearchPass.Full);

        Assert.Equal(candidates.Count > 0 ? MatchStatus.Candidates : MatchStatus.Failed, status);
        Assert.Subset(candidates.ToHashSet(), matches.ToHashSet());
    }

    [Fact]
    public void Evaluate_QuickPass_LeavesOutSomeSeedsAndKeepsOthers()
    {
        var condition = Village(SearchArea.Square(64));
        var statuses = Enumerable.Range(0, 200).Select(seed => _bench.Evaluate(condition, Version, seed, SearchPass.Quick).Status).ToList();

        Assert.Contains(MatchStatus.Failed, statuses);
        Assert.Contains(MatchStatus.Candidates, statuses);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Evaluate_Circle_KeepsOnlyTheStructuresWithinItsRadius(long seed)
    {
        var reference = new BlockPos(3000, -1500);
        var condition = Village(SearchArea.Circle(Side));
        var expected = FindVillages(seed, condition.Area, reference).Where(village => village.DistanceTo(reference) <= Side).ToList();

        var (status, matches) = _bench.Evaluate(condition, Version, seed, SearchPass.Full, reference);

        Assert.Equal(expected.Count > 0 ? MatchStatus.Matched : MatchStatus.Failed, status);
        Assert.Equal(expected, matches);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Evaluate_Count_AsksForThatManyStructures(long seed)
    {
        var condition = Village(SearchArea.Square(Side)) with { Count = 3 };
        var expected = FindVillages(seed, condition.Area, ConditionTestBench.Origin);

        var (status, _) = _bench.Evaluate(condition, Version, seed, SearchPass.Full);

        Assert.Equal(expected.Count >= 3 ? MatchStatus.Matched : MatchStatus.Failed, status);
    }

    [Theory]
    [MemberData(nameof(Seeds))]
    public void Evaluate_Exclusion_HoldsAtItsReferencePointWhenTheAreaHasNoStructure(long seed)
    {
        var reference = new BlockPos(-640, 320);
        var condition = Village(SearchArea.Square(1024)) with { Count = 0 };
        var villages = FindVillages(seed, condition.Area, reference);

        var (quick, quickMatches) = _bench.Evaluate(condition, Version, seed, SearchPass.Quick, reference);
        var (full, fullMatches) = _bench.Evaluate(condition, Version, seed, SearchPass.Full, reference);

        Assert.Equal(villages.Count == 0 ? MatchStatus.Matched : MatchStatus.Failed, full);
        Assert.NotEqual(MatchStatus.Failed, quick);
        Assert.Equal([reference], quickMatches);
        if (full == MatchStatus.Matched)
            Assert.Equal([reference], fullMatches);
        if (quick == MatchStatus.Matched)
            Assert.Equal(MatchStatus.Matched, full);
    }

    [Fact]
    public void Evaluate_NetherStructure_LooksInTheNether()
    {
        const long seed = 31;
        var condition = new StructureCondition { Id = 1, Type = StructureType.Fortress, Area = SearchArea.Square(Side) };
        var area = condition.Area.ToBlocks(ConditionTestBench.Origin);
        var expected = _bench.GetGenerator(Version, seed, Dimension.Nether).FindStructures(StructureType.Fortress, area);

        var (_, matches) = _bench.Evaluate(condition, Version, seed, SearchPass.Full);

        Assert.NotEmpty(expected);
        Assert.Equal(expected, matches);
    }

    [Theory]
    [InlineData(StructureType.EndCity, StructureTrait.EndShip, Dimension.End, 3000)]
    [InlineData(StructureType.Village, StructureTrait.Abandoned, Dimension.Overworld, Side)]
    [InlineData(StructureType.Igloo, StructureTrait.Basement, Dimension.Overworld, 4096)]
    [InlineData(StructureType.RuinedPortal, StructureTrait.Giant, Dimension.Overworld, Side)]
    public void Evaluate_Trait_KeepsTheStructuresTheGeneratorDescribesWithIt(StructureType type, StructureTrait trait, Dimension dimension, int side)
    {
        var withTrait = 0;
        var without = 0;
        for (long seed = 0; seed < 40; seed++)
        {
            var condition = new StructureCondition { Id = 1, Type = type, Trait = trait, Area = SearchArea.Square(side) };
            var generator = _bench.GetGenerator(Version, seed, dimension);
            var structures = generator.FindStructures(type, condition.Area.ToBlocks(ConditionTestBench.Origin));
            var expected = structures.Where(position => trait.IsIn(generator.DescribeStructure(type, position))).ToList();

            var (status, matches) = _bench.Evaluate(condition, Version, seed, SearchPass.Full);

            Assert.Equal(expected.Count > 0 ? MatchStatus.Matched : MatchStatus.Failed, status);
            Assert.Equal(expected, matches);
            withTrait += expected.Count;
            without += structures.Length - expected.Count;
        }

        Assert.True(withTrait > 0, "No structure had the trait.");
        Assert.True(without > 0, "Every structure had the trait.");
    }

    [Theory]
    [InlineData(StructureType.EndCity, StructureTrait.Abandoned, "End city has no such kind.")]
    [InlineData(StructureType.Village, (StructureTrait)99, "Village has no such kind.")]
    public void Validate_TraitTheTypeCannotHave_IsAProblem(StructureType type, StructureTrait trait, string expected)
    {
        var condition = new StructureCondition { Id = 1, Type = type, Trait = trait };

        var problems = TestEngine.CreateConditionValidator().Validate(ConditionTestBench.Define(Version, condition));

        Assert.Equal(expected, Assert.Single(problems).Message);
    }

    [Fact]
    public void Evaluate_StructureTheVersionDoesNotHave_Fails()
    {
        var condition = new StructureCondition { Id = 1, Type = StructureType.TrialChambers };

        Assert.Equal(MatchStatus.Failed, _bench.Evaluate(condition, Version, 5, SearchPass.Quick).Status);
        Assert.Equal(MatchStatus.Failed, _bench.Evaluate(condition, Version, 5, SearchPass.Full).Status);
    }

    private List<BlockPos> FindVillages(long seed, SearchArea area, BlockPos reference)
        => [.. _bench.GetGenerator(Version, seed).FindStructures(StructureType.Village, area.ToBlocks(reference))];

    private static StructureCondition Village(SearchArea area) => new() { Id = 1, Type = StructureType.Village, Area = area };
}

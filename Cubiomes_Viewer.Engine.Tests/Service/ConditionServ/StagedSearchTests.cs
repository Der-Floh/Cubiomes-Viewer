using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ConditionServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

/// <summary>The matcher against real worlds: the quick pass may only spare work, never change which seeds are found.</summary>
public sealed class StagedSearchTests : IDisposable
{
    private const McVersion Version = McVersion.V1_16_5;
    private const int SeedsPerDefinition = 40;
    private const int Plains = 1;
    private const int Desert = 2;
    private const int CherryGrove = 185;

    private static readonly Dictionary<string, SearchDefinition> _definitions = new(StringComparer.Ordinal)
    {
        ["a village"] = Define(Village(1, 1024)),
        ["three villages"] = Define(Village(1, 3072) with { Count = 3 }),
        ["no village"] = Define(Village(1, 768) with { Count = 0 }),
        ["many slime chunks"] = Define(new SlimeChunkCondition { Id = 1, Area = SearchArea.Square(144), Count = 10 }),
        ["a village with a ruined portal nearby"] = Define(Village(1, 2048), Structure(2, StructureType.RuinedPortal, 600, relativeTo: 1)),
        ["the nearest village has a ruined portal nearby"] = Define(Village(1, 2048) with { Anchor = MatchAnchor.Nearest }, Structure(2, StructureType.RuinedPortal, 700, relativeTo: 1)),
        ["slime chunks in the middle of the villages"] = Define(
            Village(1, 3072) with { Count = 2, Anchor = MatchAnchor.Center },
            new SlimeChunkCondition { Id = 2, RelativeTo = 1, Area = SearchArea.Square(144), Count = 8 }),
        ["a temple of any kind"] = Define(
            new AnyOfCondition { Id = 1 },
            Structure(2, StructureType.DesertPyramid, 1536, relativeTo: 1),
            Structure(3, StructureType.SwampHut, 1536, relativeTo: 1),
            Structure(4, StructureType.JungleTemple, 1536, relativeTo: 1)),
        ["plains by the nearest village"] = Define(
            Village(1, 1536) with { Anchor = MatchAnchor.Nearest },
            new BiomeCondition { Id = 2, RelativeTo = 1, Biomes = BiomeSet.Of(Plains, Desert), Area = SearchArea.Square(96) }),
        ["a stronghold near the spawn point"] = Define(
            new SpawnCondition { Id = 1, Area = SearchArea.Circle(400) },
            new StrongholdCondition { Id = 2, RelativeTo = 1, Area = SearchArea.Circle(1900) }),
        ["a village without a swamp hut around"] = Define(
            Village(1, 2048),
            Structure(2, StructureType.SwampHut, 3000, relativeTo: 1) with { Count = 0 },
            new SlimeChunkCondition { Id = 3, RelativeTo = 2, Area = SearchArea.Square(144), Count = 7 }),
        ["a fortress with a bastion nearby"] = Define(Structure(1, StructureType.Fortress, 1024), Structure(2, StructureType.Bastion, 700, relativeTo: 1)),
        ["no village, or slime chunks instead"] = Define(
            Village(1, 600) with { Count = 0 },
            new AnyOfCondition { Id = 2, RelativeTo = 1 },
            new SlimeChunkCondition { Id = 3, RelativeTo = 2, Area = SearchArea.Square(144), Count = 11 },
            Structure(4, StructureType.RuinedPortal, 500, relativeTo: 2)),
        ["a village with trial chambers nearby in 1.21"] = Define(Village(1, 1536), Structure(2, StructureType.TrialChambers, 700, relativeTo: 1)) with { Version = McVersion.V1_21_3 },
        ["a cherry grove in 1.21"] = Define(new BiomeCondition { Id = 1, Biomes = BiomeSet.Of(CherryGrove), Area = SearchArea.Circle(1024), Scale = 64 }) with { Version = McVersion.V1_21_3 },
        ["far inland on average in 1.21"] = Define(new ClimateCondition { Id = 1, Parameter = ClimateParameter.Continentalness, Min = 0.3, Max = 1, Match = ClimateMatch.OnAverage, Area = SearchArea.Square(1024) }) with { Version = McVersion.V1_21_3 },
        ["a village with a coast nearby in 1.21"] = Define(
            Village(1, 1536),
            new ClimateCondition { Id = 2, RelativeTo = 1, Parameter = ClimateParameter.Continentalness, Min = -0.19, Max = -0.11, Match = ClimateMatch.Somewhere, Area = SearchArea.Circle(256), Scale = 16 }) with { Version = McVersion.V1_21_3 },
    };

    private readonly ConditionTestBench _bench = new();
    private readonly SearchPlanCompiler _compiler = TestEngine.CreateSearchPlanCompiler();
    private readonly SeedMatcher _matcher = new();

    public void Dispose() => _bench.Dispose();

    public static TheoryData<string> DefinitionNames() => [.. _definitions.Keys];

    [Theory]
    [MemberData(nameof(DefinitionNames))]
    public void IsMatch_RandomSeeds_FindsExactlyTheSeedsTheFullPassFinds(string name)
    {
        var definition = _definitions[name];
        var plan = _compiler.Compile(definition);
        var random = new Random(name.Sum(character => character));
        var found = 0;

        Assert.Empty(TestEngine.CreateConditionValidator().Validate(definition));
        for (var index = 0; index < SeedsPerDefinition; index++)
        {
            var context = _bench.Open(definition, random.NextInt64(long.MinValue, long.MaxValue));
            var quick = _matcher.Evaluate(plan, context, SearchPass.Quick);
            var full = _matcher.Evaluate(plan, context, SearchPass.Full);

            Assert.NotEqual(SearchOutcome.Undecided, full);
            if (quick != SearchOutcome.Undecided)
                Assert.Equal(full, quick);
            Assert.Equal(full == SearchOutcome.Passed, _matcher.IsMatch(plan, context));
            found += full == SearchOutcome.Passed ? 1 : 0;
        }

        Assert.InRange(found, 1, SeedsPerDefinition - 1);
    }

    [Theory]
    [MemberData(nameof(DefinitionNames))]
    public void Evaluate_QuickPass_ComesOutTheSameForAllSisterSeeds(string name)
    {
        var definition = _definitions[name];
        var plan = _compiler.Compile(definition);
        var random = new Random(name.Length);

        for (var index = 0; index < SeedsPerDefinition; index++)
        {
            var structureSeed = random.NextInt64(StructureSeed.Max);
            var expected = _matcher.Evaluate(plan, _bench.Open(definition, structureSeed), SearchPass.Quick);

            foreach (var upperBits in new[] { 1, random.Next(StructureSeed.SisterSeeds), StructureSeed.SisterSeeds - 1 })
                Assert.Equal(expected, _matcher.Evaluate(plan, _bench.Open(definition, StructureSeed.ToWorldSeed(structureSeed, upperBits)), SearchPass.Quick));
        }
    }

    [Fact]
    public void IsMatch_VillageWithARuinedPortalNearby_AgreesWithLookingForBothByHand()
    {
        var definition = _definitions["a village with a ruined portal nearby"];
        var plan = _compiler.Compile(definition);
        var villageArea = definition.Conditions[0].Area;
        var portalArea = definition.Conditions[1].Area;

        for (var seed = 500L; seed < 560; seed++)
        {
            var generator = _bench.GetGenerator(Version, seed);
            var villages = generator.FindStructures(StructureType.Village, villageArea.ToBlocks(ConditionTestBench.Origin));
            var expected = villages.Any(village => generator.FindStructures(StructureType.RuinedPortal, portalArea.ToBlocks(village)).Length > 0);

            Assert.Equal(expected, _matcher.IsMatch(plan, _bench.Open(definition, seed)));
        }
    }

    [Fact]
    public void IsMatch_AnyOf_FindsTheSeedsOneOfItsConditionsFinds()
    {
        var anyOf = _definitions["a temple of any kind"];
        var alternatives = anyOf.Conditions.Skip(1).Select(condition => Define(condition with { RelativeTo = null })).ToList();

        for (var seed = 900L; seed < 960; seed++)
        {
            var expected = alternatives.Any(alternative => IsMatch(alternative, seed));

            Assert.Equal(expected, IsMatch(anyOf, seed));
        }
    }

    [Fact]
    public void IsMatch_Exclusion_FindsTheSeedsItsOppositeDoesNotFind()
    {
        var without = _definitions["no village"];
        var with = Define(without.Conditions[0] with { Count = 1 });

        for (var seed = 100L; seed < 160; seed++)
            Assert.NotEqual(IsMatch(with, seed), IsMatch(without, seed));
    }

    private bool IsMatch(SearchDefinition definition, long seed) => _matcher.IsMatch(_compiler.Compile(definition), _bench.Open(definition, seed));

    private static SearchDefinition Define(params SearchCondition[] conditions) => ConditionTestBench.Define(Version, conditions);

    private static StructureCondition Village(int id, int side) => Structure(id, StructureType.Village, side);

    private static StructureCondition Structure(int id, StructureType type, int side, int? relativeTo = null)
        => new() { Id = id, RelativeTo = relativeTo, Type = type, Area = SearchArea.Square(side) };
}

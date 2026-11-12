using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ConditionServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

public sealed class ConditionValidatorTests
{
    private const int Plains = 1;
    private const int NetherWastes = 8;
    private const int CrimsonForest = 171;
    private const int UnknownBiome = 250;

    private readonly ConditionValidator _validator = TestEngine.CreateConditionValidator();

    [Fact]
    public void Validate_EveryKindOfConditionSetUpWell_FindsNoProblem()
    {
        var definition = Define(
            Village(1),
            new StructureCondition { Id = 2, RelativeTo = 1, Type = StructureType.RuinedPortal, Area = SearchArea.Circle(300) },
            new AnyOfCondition { Id = 3 },
            new BiomeCondition { Id = 4, RelativeTo = 3, Biomes = BiomeSet.Of(Plains) },
            new SlimeChunkCondition { Id = 5, RelativeTo = 3, Count = 4 },
            new SpawnCondition { Id = 6 },
            new StrongholdCondition { Id = 7, Count = 0 });

        Assert.Empty(_validator.Validate(definition));
    }

    [Fact]
    public void Validate_NoConditions_AsksForOne()
    {
        var problem = Assert.Single(_validator.Validate(Define()));

        Assert.Equal(0, problem.ConditionId);
        Assert.Equal("Add at least one condition.", problem.ToString());
    }

    [Fact]
    public void Validate_UnknownVersion_AsksForAVersion()
    {
        var problem = Assert.Single(_validator.Validate(Define(Village(1)) with { Version = McVersion.Undefined }));

        Assert.Equal(0, problem.ConditionId);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(100)]
    public void Validate_NumberOutOfRange_IsAProblem(int id)
        => AssertProblem(Define(Village(id)), id, "between 1 and 99");

    [Fact]
    public void Validate_TwoConditionsWithTheSameNumber_AreBothProblems()
    {
        var problems = _validator.Validate(Define(Village(1), new SpawnCondition { Id = 1 }));

        Assert.Equal(2, problems.Count);
        Assert.All(problems, problem => Assert.Contains("same number", problem.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_RelativeToItself_IsAProblem()
        => AssertProblem(Define(Village(1, relativeTo: 1)), 1, "relative to itself");

    [Fact]
    public void Validate_RelativeToAMissingCondition_IsAProblem()
        => AssertProblem(Define(Village(1, relativeTo: 7)), 1, "condition 7, which does not exist");

    [Fact]
    public void Validate_ConditionsRelativeToEachOther_AreProblemsWithEveryConditionThatDependsOnThem()
    {
        var problems = _validator.Validate(Define(Village(1, relativeTo: 2), Village(2, relativeTo: 1), Village(3, relativeTo: 2), Village(4)));

        Assert.Equal([1, 2, 3], problems.Select(problem => problem.ConditionId));
        Assert.All(problems, problem => Assert.Contains("through other conditions", problem.Message, StringComparison.Ordinal));
    }

    [Fact]
    public void Validate_AreaWithoutSize_IsAProblem()
        => AssertProblem(Define(Village(1) with { Area = new SearchArea(10, 0, 5, 20) }), 1, "no size");

    [Fact]
    public void Validate_AreaTooFarFromItsReferencePoint_IsAProblem()
        => AssertProblem(Define(new SpawnCondition { Id = 1, Area = SearchArea.Rectangle(0, 0, SearchArea.MaxOffset + 1, 10) }), 1, "at most 10,000,000 blocks");

    [Fact]
    public void Validate_MoreMatchesThanTheKindCanHave_IsAProblem()
        => AssertProblem(Define(new SpawnCondition { Id = 1, Count = 2 }), 1, "At most 1 matches");

    [Fact]
    public void Validate_NegativeCount_IsAProblem()
        => AssertProblem(Define(Village(1) with { Count = -1 }), 1, "cannot be negative");

    [Fact]
    public void Validate_AnyOfAskingForNoMatch_IsAProblem()
        => AssertProblem(Define(new AnyOfCondition { Id = 1, Count = 0 }, Village(2, relativeTo: 1)), 1, "cannot ask for no match");

    [Fact]
    public void Validate_AnyOfWithoutRelativeConditions_IsAProblem()
        => AssertProblem(Define(new AnyOfCondition { Id = 1 }, Village(2)), 1, "at least one condition relative to this one");

    [Fact]
    public void Validate_StructureTheVersionDoesNotHave_IsAProblem()
    {
        var definition = Define(new StructureCondition { Id = 1, Type = StructureType.TrialChambers }) with { Version = McVersion.V1_12_2 };

        AssertProblem(definition, 1, "does not generate in this version");
    }

    [Fact]
    public void Validate_StructureAreaOfTooManyRegions_IsAProblem()
        => AssertProblem(Define(new StructureCondition { Id = 1, Type = StructureType.Mineshaft, Area = SearchArea.Square(10_000) }), 1, "too large");

    [Fact]
    public void Validate_BiomeConditionWithoutBiomes_IsAProblem()
        => AssertProblem(Define(new BiomeCondition { Id = 1, Biomes = BiomeSet.Empty }), 1, "at least one biome");

    [Fact]
    public void Validate_BiomeTheVersionDoesNotHave_IsAProblem()
        => AssertProblem(Define(new BiomeCondition { Id = 1, Biomes = BiomeSet.Of(Plains, UnknownBiome) }), 1, "Biome 250 does not generate in the Overworld");

    [Fact]
    public void Validate_BiomeOfAnotherDimension_IsAProblem()
        => AssertProblem(Define(new BiomeCondition { Id = 1, Biomes = BiomeSet.Of(Plains, NetherWastes) }), 1, "Nether Wastes does not generate in the Overworld");

    [Fact]
    public void Validate_BiomeOfTheDimensionTheConditionLooksIn_IsNoProblem()
        => Assert.Empty(_validator.Validate(Define(new BiomeCondition { Id = 1, Biomes = BiomeSet.Of(NetherWastes, CrimsonForest), Dimension = Dimension.Nether })));

    [Fact]
    public void Validate_BiomeThatCameLater_IsAProblemInOlderVersions()
    {
        var definition = Define(new BiomeCondition { Id = 1, Biomes = BiomeSet.Of(CrimsonForest), Dimension = Dimension.Nether }) with { Version = McVersion.V1_15_2 };

        AssertProblem(definition, 1, "Crimson Forest does not generate in the Nether");
    }

    [Fact]
    public void Validate_BiomeSampleDistanceTheGeneratorDoesNotHave_IsAProblem()
        => AssertProblem(Define(new BiomeCondition { Id = 1, Biomes = BiomeSet.Of(Plains), Scale = 8 }), 1, "one of 1, 4, 16, 64, 256");

    [Fact]
    public void Validate_BiomeAreaOfTooManySamples_IsAProblem()
        => AssertProblem(Define(new BiomeCondition { Id = 1, Biomes = BiomeSet.Of(Plains), Scale = 1, Area = SearchArea.Square(2048) }), 1, "more than 262144 samples");

    [Fact]
    public void Validate_SpawnOfAVersionThatCannotPredictIt_IsAProblem()
        => AssertProblem(Define(new SpawnCondition { Id = 1 }) with { Version = McVersion.Beta1_7 }, 1, "cannot be predicted");

    [Fact]
    public void Validate_StrongholdOfAVersionWithoutThem_IsAProblem()
        => AssertProblem(Define(new StrongholdCondition { Id = 1 }) with { Version = McVersion.Beta1_7 }, 1, "no strongholds");

    private void AssertProblem(SearchDefinition definition, int conditionId, string messagePart)
    {
        var problem = Assert.Single(_validator.Validate(definition));

        Assert.Equal(conditionId, problem.ConditionId);
        Assert.Contains(messagePart, problem.Message, StringComparison.Ordinal);
    }

    private static SearchDefinition Define(params SearchCondition[] conditions) => new() { Version = McVersion.V1_21_3, Conditions = conditions };

    private static StructureCondition Village(int id, int? relativeTo = null) => new() { Id = id, RelativeTo = relativeTo, Type = StructureType.Village };
}

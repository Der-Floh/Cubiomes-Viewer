using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ConditionServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.ConditionServ;

public sealed class SearchPlanCompilerTests
{
    private readonly SearchPlanCompiler _compiler = TestEngine.CreateSearchPlanCompiler();

    [Fact]
    public void Compile_Roots_AreOrderedFromCheapToCostly()
    {
        var plan = _compiler.Compile(Define(Biome(1), new SpawnCondition { Id = 2 }, Village(3), new SlimeChunkCondition { Id = 4 }, new StrongholdCondition { Id = 5 }));

        Assert.Equal([4, 3, 2, 5, 1], plan.Roots.Select(node => node.Condition.Id));
    }

    [Fact]
    public void Compile_RelativeConditions_HangBelowTheConditionTheyAreRelativeTo()
    {
        var plan = _compiler.Compile(Define(Village(1), Biome(2, relativeTo: 1), Village(3, relativeTo: 1), new SlimeChunkCondition { Id = 4, RelativeTo = 3 }));

        var root = Assert.Single(plan.Roots);
        Assert.Equal([3, 2], root.Children.Select(node => node.Condition.Id));
        Assert.Equal(4, Assert.Single(root.Children[0].Children).Condition.Id);
        Assert.Empty(root.Children[1].Children);
    }

    [Fact]
    public void Compile_AnyOf_CostsAsMuchAsItsCheapestAlternative()
    {
        var plan = _compiler.Compile(Define(new AnyOfCondition { Id = 1 }, Biome(2, relativeTo: 1), new StrongholdCondition { Id = 3, RelativeTo = 1 }, Village(4)));

        Assert.Equal([4, 1], plan.Roots.Select(node => node.Condition.Id));
        Assert.True(plan.Roots[1].AcceptsAnyChild);
        Assert.Equal(plan.Roots[1].Children[0].Cost, plan.Roots[1].Cost);
    }

    [Fact]
    public void Compile_RelativeToAMissingCondition_Throws()
        => Assert.Throws<ArgumentException>(() => _compiler.Compile(Define(Village(1, relativeTo: 2))));

    [Fact]
    public void Compile_ConditionsRelativeToEachOther_Throws()
        => Assert.Throws<ArgumentException>(() => _compiler.Compile(Define(Village(1, relativeTo: 2), Village(2, relativeTo: 1), Village(3))));

    [Fact]
    public void Compile_TwoConditionsWithTheSameNumber_Throws()
        => Assert.Throws<ArgumentException>(() => _compiler.Compile(Define(Village(1), Village(1, relativeTo: 1))));

    private static SearchDefinition Define(params SearchCondition[] conditions) => new() { Version = McVersion.V1_21_3, Conditions = conditions };

    private static StructureCondition Village(int id, int? relativeTo = null) => new() { Id = id, RelativeTo = relativeTo, Type = StructureType.Village };

    private static BiomeCondition Biome(int id, int? relativeTo = null) => new() { Id = id, RelativeTo = relativeTo, Biomes = BiomeSet.Of(1) };
}

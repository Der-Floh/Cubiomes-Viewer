using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

namespace Cubiomes_Viewer.Engine.Tests.Data.Search;

public sealed class SearchDefinitionTests
{
    [Fact]
    public void Equals_SameConditions_IsEqual()
    {
        Assert.Equal(CreateDefinition(), CreateDefinition());
        Assert.Equal(CreateDefinition().GetHashCode(), CreateDefinition().GetHashCode());
    }

    [Fact]
    public void Equals_OneConditionDiffers_IsNotEqual()
    {
        var definition = CreateDefinition();
        var changed = definition with { Conditions = [.. definition.Conditions.Skip(1)] };

        Assert.NotEqual(definition, changed);
    }

    [Fact]
    public void Flags_LargeBiomes_KeepTheOceanVariantsOfTheMap()
    {
        Assert.Equal(GeneratorFlags.ForceOceanVariants, new SearchDefinition().Flags);
        Assert.Equal(GeneratorFlags.ForceOceanVariants | GeneratorFlags.LargeBiomes, new SearchDefinition { LargeBiomes = true }.Flags);
    }

    [Fact]
    public void SearchBatchJob_EveryKindOfCondition_RoundTripsThroughTheEnvelope()
    {
        var job = new SearchBatchJob(CreateDefinition(), long.MinValue + 5, 1000);

        var restored = ComputeEnvelope.DeserializeJob(ComputeEnvelope.SerializeJob(job));

        Assert.Equal(job, restored);
    }

    private static SearchDefinition CreateDefinition() => new()
    {
        Version = McVersion.V1_16_5,
        LargeBiomes = true,
        Conditions =
        [
            new StructureCondition { Id = 1, Type = StructureType.Village, Area = SearchArea.Circle(500), Count = 2, Anchor = MatchAnchor.Nearest },
            new BiomeCondition { Id = 2, RelativeTo = 1, Biomes = BiomeSet.Of(1, 129), Match = BiomeMatch.All, Dimension = Dimension.Overworld, Scale = 4, Y = 80 },
            new SpawnCondition { Id = 3, Area = SearchArea.Rectangle(-10, -20, 30, 40) },
            new StrongholdCondition { Id = 4, Count = 0 },
            new AnyOfCondition { Id = 5 },
            new SlimeChunkCondition { Id = 6, RelativeTo = 5, Count = 12, Anchor = MatchAnchor.Center },
        ],
    };
}

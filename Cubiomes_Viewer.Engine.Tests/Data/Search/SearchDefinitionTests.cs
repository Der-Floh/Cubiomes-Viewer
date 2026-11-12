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
        Assert.Equal(SampleDefinitions.CreateEveryKindOfCondition(), SampleDefinitions.CreateEveryKindOfCondition());
        Assert.Equal(SampleDefinitions.CreateEveryKindOfCondition().GetHashCode(), SampleDefinitions.CreateEveryKindOfCondition().GetHashCode());
    }

    [Fact]
    public void Equals_OneConditionDiffers_IsNotEqual()
    {
        var definition = SampleDefinitions.CreateEveryKindOfCondition();
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
        var job = new SearchBatchJob(SampleDefinitions.CreateEveryKindOfCondition(), long.MinValue + 5, 1000);

        var restored = ComputeEnvelope.DeserializeJob(ComputeEnvelope.SerializeJob(job));

        Assert.Equal(job, restored);
    }

    [Fact]
    public void SearchBatchJob_WithStrideOrSeeds_RoundTripsThroughTheEnvelope()
    {
        SearchBatchJob[] jobs =
        [
            new(SampleDefinitions.CreateEveryKindOfCondition(), 0x1234, 64, 1L << 48),
            new(SampleDefinitions.CreateEveryKindOfCondition(), 5, 3, Seeds: [5, -9, long.MaxValue]),
            new SearchBatchJob(SampleDefinitions.CreateEveryKindOfCondition(), 9, 100) { MaxMatches = 3, MaxMilliseconds = 40 },
        ];

        Assert.All(jobs, job => Assert.Equal(job, ComputeEnvelope.DeserializeJob(ComputeEnvelope.SerializeJob(job))));
        Assert.NotEqual(jobs[^1], jobs[^1] with { MaxMatches = 4 });
    }

    [Fact]
    public void StructureSeedBatchJob_RoundTripsThroughTheEnvelope()
    {
        var job = new StructureSeedBatchJob(SampleDefinitions.CreateEveryKindOfCondition(), (1L << 48) - 3, 65_000, 200_000) { MaxMatches = 7, MaxMilliseconds = 250 };

        var restored = ComputeEnvelope.DeserializeJob(ComputeEnvelope.SerializeJob(job));

        Assert.Equal(job, restored);
    }
}

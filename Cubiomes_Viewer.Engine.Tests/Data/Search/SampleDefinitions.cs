using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Tests.Data.Search;

internal static class SampleDefinitions
{
    /// <summary>A definition with every kind of condition and every setting changed from its default.</summary>
    public static SearchDefinition CreateEveryKindOfCondition() => new()
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
            new QuadHutCondition { Id = 7, Type = StructureType.Igloo, Quality = QuadQuality.Classic, Count = 2 },
        ],
    };
}

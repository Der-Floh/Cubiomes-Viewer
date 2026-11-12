using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.SearchServ;

/// <summary>The presets the app comes with, to show what a search can look for.</summary>
public static class ExampleSearchPresets
{
    private const int MushroomFields = 14;
    private const int BiomeScale = 16;

    public static IReadOnlyList<SearchPreset> All { get; } =
    [
        new()
        {
            Name = "Quad witch huts near spawn",
            Description = "Four swamp huts within reach of one spot around the world origin, for a witch farm. Tries only the seeds whose huts can form a classic quad.",
            Setup = new SearchSetup
            {
                Definition = Define(new QuadHutCondition { Id = 1, Quality = QuadQuality.Classic, Area = SearchArea.Square(2048) }),
                Source = new QuadHutSeedSource { Quality = QuadQuality.Classic },
                MaxResults = 20,
            },
        },
        new()
        {
            Name = "Village by the spawn point",
            Description = "A village within 256 blocks of where the world spawns you.",
            Setup = new SearchSetup
            {
                Definition = Define(
                    new SpawnCondition { Id = 1, Area = SearchArea.Square(2048) },
                    new StructureCondition { Id = 2, RelativeTo = 1, Type = StructureType.Village, Area = SearchArea.Circle(256) }),
            },
        },
        new()
        {
            Name = "Mushroom fields near spawn",
            Description = "Mushroom fields, where no hostile mobs spawn, within 1,024 blocks of the world origin.",
            Setup = new SearchSetup
            {
                Definition = Define(new BiomeCondition { Id = 1, Biomes = BiomeSet.Of(MushroomFields), Area = SearchArea.Circle(1024), Scale = BiomeScale }),
            },
        },
        new()
        {
            Name = "Slime chunks at the origin",
            Description = "At least 8 of the 25 chunks around the world origin are slime chunks, for a slime farm.",
            Setup = new SearchSetup
            {
                Definition = Define(new SlimeChunkCondition { Id = 1, Area = SearchArea.Square(80), Count = 8 }),
            },
        },
        new()
        {
            Name = "Stronghold close to the origin",
            Description = "A stronghold within 1,500 blocks of the world origin, for an early trip to the End.",
            Setup = new SearchSetup
            {
                Definition = Define(new StrongholdCondition { Id = 1, Area = SearchArea.Circle(1500) }),
            },
        },
    ];

    private static SearchDefinition Define(params SearchCondition[] conditions) => new() { Conditions = conditions };
}

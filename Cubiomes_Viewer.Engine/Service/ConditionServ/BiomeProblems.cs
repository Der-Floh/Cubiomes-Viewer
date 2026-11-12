using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>What keeps the biomes of a condition from being looked for in a dimension of a version.</summary>
internal static class BiomeProblems
{
    public static IEnumerable<string> Find(BiomeSet biomes, Dimension dimension, McVersion version, IBiomeCatalog catalog)
    {
        if (biomes.IsEmpty)
            yield return "Choose at least one biome.";

        if (!Enum.IsDefined(dimension))
        {
            yield return "Choose the dimension the biomes are in.";
            yield break;
        }

        foreach (var biome in biomes.ToArray().Where(biome => !catalog.Generates(version, dimension, biome)))
            yield return $"{catalog.GetDisplayName(version, biome) ?? $"Biome {biome}"} does not generate in the {dimension} of this version.";
    }
}

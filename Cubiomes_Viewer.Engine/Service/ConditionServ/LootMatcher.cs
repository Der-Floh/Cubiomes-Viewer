using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>Tells whether the containers of a structure meet a loot requirement.</summary>
internal static class LootMatcher
{
    public static bool Holds(IWorldGenerator generator, StructureType type, BlockPos position, LootRequirement requirement)
        => generator.CountLoot(type, position, requirement.ToQuery()) is { } count && requirement.IsMetBy(count);
}

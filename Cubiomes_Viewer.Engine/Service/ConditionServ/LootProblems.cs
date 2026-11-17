using Cubiomes_Viewer.Engine.Data.Loot;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.LootServ;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>What keeps the containers of a structure type from being searched for an item in a version.</summary>
internal static class LootProblems
{
    public static IEnumerable<string> Find(LootRequirement requirement, StructureType type, string structureName, McVersion version, ILootCatalog catalog)
    {
        if (!catalog.HasLoot(type, version))
        {
            yield return $"{structureName} chests cannot be read in this version.";
            yield break;
        }

        if (requirement.MinCount is < 1 or > LootRequirement.MaxCount)
            yield return $"Ask for 1 to {LootRequirement.MaxCount} items.";
        if (!Enum.IsDefined(requirement.Scope))
            yield return "Choose whether the items are in one chest or in all chests together.";

        if (string.IsNullOrEmpty(requirement.Item))
        {
            yield return "Choose the item the chests hold.";
            yield break;
        }

        var item = catalog.GetItems(type, version).FirstOrDefault(candidate => candidate.Id == requirement.Item);
        if (item is null)
            yield return $"{structureName} chests never hold {catalog.GetItemName(requirement.Item)} in this version.";
        else if (requirement.Enchantment is { } enchantment && FindEnchantmentProblem(enchantment, item, type, structureName, version, catalog) is { } problem)
            yield return problem;
    }

    private static string? FindEnchantmentProblem(LootEnchantment enchantment, LootItem item, StructureType type, string structureName, McVersion version, ILootCatalog catalog)
    {
        if (!item.CanBeEnchanted)
            return $"{structureName} chests never hold an enchanted {item.Name}.";

        var option = catalog.GetEnchantments(type, version, item.Id).FirstOrDefault(candidate => candidate.Id == enchantment.Id);
        var enchantmentName = catalog.GetEnchantmentName(enchantment.Id);
        if (option is null)
            return $"{structureName} chests never put {enchantmentName} on {item.Name}.";
        if (enchantment.MinLevel < 1)
            return "Enchantment levels start at 1.";

        return enchantment.MinLevel > option.MaxLevel ? $"{structureName} chests put {enchantmentName} on {item.Name} up to level {option.MaxLevel}." : null;
    }
}

using Cubiomes_Viewer.Engine.Data.Loot;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.LootServ;

/// <summary>What the containers of each structure type can hold, and the English names of items, enchantments and effects.</summary>
public interface ILootCatalog
{
    /// <summary>Whether the generator can tell what the containers of a structure type hold in a version.</summary>
    bool HasLoot(StructureType type, McVersion version);

    /// <summary>The items the containers of a structure type can hold in a version, ordered by name; empty without loot.</summary>
    IReadOnlyList<LootItem> GetItems(StructureType type, McVersion version);

    /// <summary>The enchantments the containers of a structure type put on an item in a version, ordered by name.</summary>
    IReadOnlyList<LootEnchantmentOption> GetEnchantments(StructureType type, McVersion version, string item);

    /// <summary>The English name of an item, such as "Heart of the Sea".</summary>
    string GetItemName(string item);

    /// <summary>The English name of an enchantment, such as "Sharpness".</summary>
    string GetEnchantmentName(string enchantment);

    /// <summary>The English name of an enchantment at a level, such as "Sharpness III", or "Mending" for one that has a single level.</summary>
    string GetEnchantmentName(string enchantment, int level);

    /// <summary>The English name of a mob effect, such as "Fire Resistance".</summary>
    string GetEffectName(string effect);
}

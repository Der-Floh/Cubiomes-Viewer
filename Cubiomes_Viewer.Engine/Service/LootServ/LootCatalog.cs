using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Globalization;

using Cubiomes_Viewer.Engine.Data.Loot;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;

namespace Cubiomes_Viewer.Engine.Service.LootServ;

public sealed class LootCatalog : ILootCatalog
{
    private const string Namespace = "minecraft:";
    private const char WordSeparator = '_';
    private const int MaxEnchantments = 64;
    private const int Supported = 1;

    private static readonly FrozenSet<string> _smallWords = FrozenSet.Create(StringComparer.Ordinal, "of", "the", "and");

    private static readonly FrozenDictionary<string, string> _irregularNames = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["minecraft:experience_bottle"] = "Bottle o' Enchanting",
        ["minecraft:tnt"] = "TNT",
        ["minecraft:binding_curse"] = "Curse of Binding",
        ["minecraft:vanishing_curse"] = "Curse of Vanishing",
    }.ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly string[] _numerals = ["I", "II", "III", "IV", "V", "VI", "VII", "VIII", "IX", "X"];

    private readonly ConcurrentDictionary<(StructureType Type, McVersion Version), bool> _hasLoot = new();
    private readonly ConcurrentDictionary<(StructureType Type, McVersion Version), LootItem[]> _items = new();
    private readonly ConcurrentDictionary<(StructureType Type, McVersion Version, int Item), LootEnchantmentOption[]> _enchantments = new();
    private readonly ConcurrentDictionary<string, string> _names = new(StringComparer.Ordinal);

    public bool HasLoot(StructureType type, McVersion version)
        => _hasLoot.GetOrAdd((type, version), static key => ReadHasLoot(key.Type, key.Version));

    public IReadOnlyList<LootItem> GetItems(StructureType type, McVersion version)
        => HasLoot(type, version) ? _items.GetOrAdd((type, version), static (key, catalog) => catalog.ReadItems(key.Type, key.Version), this) : [];

    public IReadOnlyList<LootEnchantmentOption> GetEnchantments(StructureType type, McVersion version, string item)
    {
        ArgumentNullException.ThrowIfNull(item);

        if (!HasLoot(type, version) || !TryGetEnchantableItem(item, out var native))
            return [];

        return _enchantments.GetOrAdd((type, version, native), static (key, catalog) => catalog.ReadEnchantments(key.Type, key.Version, key.Item), this);
    }

    public string GetItemName(string item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return _names.GetOrAdd(item, ToDisplayName);
    }

    public string GetEnchantmentName(string enchantment)
    {
        ArgumentNullException.ThrowIfNull(enchantment);

        return _names.GetOrAdd(enchantment, ToDisplayName);
    }

    public string GetEnchantmentName(string enchantment, int level)
    {
        var name = GetEnchantmentName(enchantment);
        if (NativeLootNames.TryGetEnchantment(enchantment, out var native) && NativeLootNames.MaxLevel(native) == 1)
            return name;

        return $"{name} {ToNumeral(level)}";
    }

    public string GetEffectName(string effect)
    {
        ArgumentNullException.ThrowIfNull(effect);

        return _names.GetOrAdd(effect, ToDisplayName);
    }

    private static bool ReadHasLoot(StructureType type, McVersion version)
        => CubiomesNativeException.ThrowIfError(CubiomesNative.LootSupported((int)type, (int)version), nameof(HasLoot)) == Supported;

    private static bool TryGetEnchantableItem(string item, out int native)
    {
        if (item == NativeLootNames.EnchantedBookItem)
        {
            native = NativeLootNames.Book;
            return true;
        }

        return NativeLootNames.TryGetItem(item, out native) && item != NativeLootNames.BookItem;
    }

    private unsafe LootItem[] ReadItems(StructureType type, McVersion version)
    {
        var capacity = CubiomesNative.LootItemCount();
        var items = new int[capacity];
        var kinds = new int[capacity];
        int count;
        fixed (int* itemBuffer = items)
        fixed (int* kindBuffer = kinds)
            count = CubiomesNative.LootItems((int)type, (int)version, itemBuffer, kindBuffer, capacity);

        if (count == (int)NativeStatus.Unsupported)
            return [];

        count = CubiomesNativeException.ThrowIfError(count, nameof(GetItems));
        List<LootItem> found = [];
        for (var index = 0; index < count; index++)
            AddItems(found, items[index], (NativeLootKinds)kinds[index], version);

        return [.. found.OrderBy(item => item.Name, StringComparer.Ordinal)];
    }

    private void AddItems(List<LootItem> found, int item, NativeLootKinds kinds, McVersion version)
    {
        var isEnchanted = kinds.HasFlag(NativeLootKinds.Enchanted);
        if (item != NativeLootNames.Book)
        {
            var id = NativeLootNames.ItemName(item, version) ?? throw new InvalidOperationException($"The native generator lists unknown item {item}.");
            found.Add(new LootItem(id, GetItemName(id), isEnchanted));
            return;
        }

        if (isEnchanted)
            found.Add(new LootItem(NativeLootNames.EnchantedBookItem, GetItemName(NativeLootNames.EnchantedBookItem), CanBeEnchanted: true));
        if (kinds.HasFlag(NativeLootKinds.Plain))
            found.Add(new LootItem(NativeLootNames.BookItem, GetItemName(NativeLootNames.BookItem), CanBeEnchanted: false));
    }

    private unsafe LootEnchantmentOption[] ReadEnchantments(StructureType type, McVersion version, int item)
    {
        var enchantments = stackalloc int[MaxEnchantments];
        var levels = stackalloc int[MaxEnchantments];
        var count = CubiomesNative.LootEnchantments((int)type, (int)version, item, enchantments, levels, MaxEnchantments);
        if (count == (int)NativeStatus.Unsupported)
            return [];

        count = CubiomesNativeException.ThrowIfError(count, nameof(GetEnchantments));
        List<LootEnchantmentOption> found = [];
        for (var index = 0; index < count; index++)
        {
            if (NativeLootNames.EnchantmentName(enchantments[index]) is { } id)
                found.Add(new LootEnchantmentOption(id, GetEnchantmentName(id), levels[index]));
        }

        return [.. found.OrderBy(option => option.Name, StringComparer.Ordinal)];
    }

    private static string ToDisplayName(string id)
    {
        if (_irregularNames.TryGetValue(id, out var irregular))
            return irregular;

        var words = (id.StartsWith(Namespace, StringComparison.Ordinal) ? id[Namespace.Length..] : id).Split(WordSeparator, StringSplitOptions.RemoveEmptyEntries);
        for (var index = 0; index < words.Length; index++)
        {
            if (index == 0 || !_smallWords.Contains(words[index]))
                words[index] = string.Concat(words[index][..1].ToUpperInvariant(), words[index].AsSpan(1));
        }

        return string.Join(' ', words);
    }

    private static string ToNumeral(int level)
        => level >= 1 && level <= _numerals.Length ? _numerals[level - 1] : level.ToString(CultureInfo.InvariantCulture);
}

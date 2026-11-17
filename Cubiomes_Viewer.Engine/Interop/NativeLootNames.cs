using System.Collections.Frozen;
using System.Runtime.InteropServices;

using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Interop;

/// <summary>
/// Maps the ids of the native loot library to the game's ids, such as <c>minecraft:diamond</c>. The library's ids shift
/// between its updates, so only the game's ids leave the engine.
/// </summary>
internal static class NativeLootNames
{
    public const string BookItem = "minecraft:book";
    public const string EnchantedBookItem = "minecraft:enchanted_book";

    private const string Namespace = "minecraft:";

    private static readonly Lazy<Names> _names = new(Names.Read);

    public static int Book => _names.Value.Book;

    /// <summary>The item's id in a version, which differs between versions where the game renamed it.</summary>
    public static string? ItemName(int item, McVersion version) => Marshal.PtrToStringUTF8(CubiomesNative.LootItemName(item, (int)version));

    /// <summary>Finds an item by its id in any version.</summary>
    public static bool TryGetItem(string name, out int item) => _names.Value.Items.TryGetValue(name, out item);

    public static string? EnchantmentName(int enchantment) => At(_names.Value.Enchantments, enchantment);

    public static bool TryGetEnchantment(string name, out int enchantment) => _names.Value.EnchantmentIds.TryGetValue(name, out enchantment);

    /// <summary>The highest level the game gives an enchantment, or 0 for an unknown one.</summary>
    public static int MaxLevel(int enchantment) => Math.Max(CubiomesNative.LootEnchantmentMaxLevel(enchantment), 0);

    public static string? EffectName(int effect) => At(_names.Value.Effects, effect);

    public static string? TableName(int table) => At(_names.Value.Tables, table);

    private static string? At(string?[] names, int index) => index >= 0 && index < names.Length ? names[index] : null;

    private sealed class Names
    {
        public required int Book { get; init; }
        public required FrozenDictionary<string, int> Items { get; init; }
        public required string?[] Enchantments { get; init; }
        public required FrozenDictionary<string, int> EnchantmentIds { get; init; }
        public required string?[] Effects { get; init; }
        public required string?[] Tables { get; init; }

        public static Names Read()
        {
            var items = ReadItems();
            var enchantments = ReadAll(CubiomesNative.LootEnchantmentCount(), static id => ToGameEnchantment(CubiomesNative.LootEnchantmentName(id)));
            return new Names
            {
                Book = items[BookItem],
                Items = items,
                Enchantments = enchantments,
                EnchantmentIds = Index(enchantments),
                Effects = ReadAll(CubiomesNative.LootEffectCount(), static id => Marshal.PtrToStringUTF8(CubiomesNative.LootEffectName(id))),
                Tables = ReadAll(CubiomesNative.LootTableCount(), static id => Marshal.PtrToStringUTF8(CubiomesNative.LootTableName(id))),
            };
        }

        private static FrozenDictionary<string, int> ReadItems()
        {
            Dictionary<string, int> items = new(StringComparer.Ordinal);
            var count = CubiomesNative.LootItemCount();
            for (var version = CubiomesNative.OldestVersion(); version <= CubiomesNative.NewestVersion(); version++)
            {
                for (var item = 0; item < count; item++)
                {
                    if (Marshal.PtrToStringUTF8(CubiomesNative.LootItemName(item, version)) is { } name)
                        items.TryAdd(name, item);
                }
            }

            return items.ToFrozenDictionary(StringComparer.Ordinal);
        }

        private static string?[] ReadAll(int count, Func<int, string?> read)
        {
            var names = new string?[Math.Max(count, 0)];
            for (var id = 0; id < names.Length; id++)
                names[id] = read(id);
            return names;
        }

        private static FrozenDictionary<string, int> Index(string?[] names)
        {
            Dictionary<string, int> ids = new(StringComparer.Ordinal);
            for (var id = 0; id < names.Length; id++)
            {
                if (names[id] is { } name)
                    ids.TryAdd(name, id);
            }

            return ids.ToFrozenDictionary(StringComparer.Ordinal);
        }

        /// <summary>The loot library names the two curses after their display names; the game ids read the other way round.</summary>
        private static string? ToGameEnchantment(nint name) => Marshal.PtrToStringUTF8(name) switch
        {
            null => null,
            "curse_of_binding" => Namespace + "binding_curse",
            "curse_of_vanishing" => Namespace + "vanishing_curse",
            var other => Namespace + other,
        };
    }
}

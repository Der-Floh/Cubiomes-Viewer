using Cubiomes_Viewer.Engine.Data.Loot;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;

namespace Cubiomes_Viewer.Engine.Service.WorldGenServ;

/// <summary>Turns the loot the native generator rolls into the engine's types, and loot queries into native ones.</summary>
internal static class NativeLootConverter
{
    private const int NoItem = -1;
    private const string BarrelTables = "barrels/";
    private const string DispenserTable = "chests/jungle_temple_dispenser";
    private const string MinecartTable = "chests/abandoned_mineshaft";

    public static LootContainer[] ToContainers(ReadOnlySpan<NativeLootContainer> containers, ReadOnlySpan<NativeLootStack> stacks, McVersion version)
    {
        var converted = new LootContainer[containers.Length];
        for (var index = 0; index < containers.Length; index++)
            converted[index] = ToContainer(containers[index], stacks, version);
        return converted;
    }

    public static NativeLootQuery ToNativeQuery(LootQuery query)
    {
        var (item, kinds) = query.Item switch
        {
            NativeLootNames.EnchantedBookItem => (NativeLootNames.Book, NativeLootKinds.Enchanted),
            NativeLootNames.BookItem => (NativeLootNames.Book, NativeLootKinds.Plain),
            _ => (NativeLootNames.TryGetItem(query.Item, out var id) ? id : NoItem, NativeLootKinds.Any),
        };

        if (query.Enchantment is null)
            return new NativeLootQuery { Item = item, Kinds = kinds };

        return NativeLootNames.TryGetEnchantment(query.Enchantment, out var enchantment)
            ? new NativeLootQuery { Item = item, Kinds = kinds & NativeLootKinds.Enchanted, Enchantment = enchantment, MinLevel = Math.Max(query.MinLevel, 1) }
            : new NativeLootQuery { Item = NoItem, Kinds = NativeLootKinds.None };
    }

    public static LootContainerKind GetContainerKind(string table) => table switch
    {
        DispenserTable => LootContainerKind.Dispenser,
        MinecartTable => LootContainerKind.MinecartWithChest,
        _ when table.StartsWith(BarrelTables, StringComparison.Ordinal) => LootContainerKind.Barrel,
        _ => LootContainerKind.Chest,
    };

    private static LootContainer ToContainer(NativeLootContainer container, ReadOnlySpan<NativeLootStack> stacks, McVersion version)
    {
        var table = NativeLootNames.TableName(container.Table)
            ?? throw new InvalidOperationException($"The native generator filled a container from unknown loot table {container.Table}.");

        var converted = new LootStack[container.StackCount];
        for (var index = 0; index < converted.Length; index++)
            converted[index] = ToStack(stacks[container.FirstStack + index], version);

        return new LootContainer
        {
            Table = table,
            Kind = GetContainerKind(table),
            Position = new BlockPos(container.X, container.Z),
            Stacks = converted,
        };
    }

    private static unsafe LootStack ToStack(NativeLootStack stack, McVersion version)
    {
        var enchantments = new LootEnchantmentLevel[Math.Clamp(stack.EnchantmentCount, 0, NativeLootStack.MaxEnchantments)];
        for (var index = 0; index < enchantments.Length; index++)
        {
            var enchantment = NativeLootNames.EnchantmentName(stack.Enchantments[index])
                ?? throw new InvalidOperationException($"The native generator rolled unknown enchantment {stack.Enchantments[index]}.");
            enchantments[index] = new LootEnchantmentLevel(enchantment, stack.Levels[index]);
        }

        var hasEffect = stack.Effect != NativeLootStack.NoEffect;
        return new LootStack
        {
            Item = GetItemName(stack.Item, enchantments.Length > 0, version),
            Count = stack.Count,
            Enchantments = enchantments,
            Effect = hasEffect ? NativeLootNames.EffectName(stack.Effect) : null,
            EffectTicks = hasEffect ? Math.Max(stack.EffectTicks, 0) : 0,
        };
    }

    private static string GetItemName(int item, bool isEnchanted, McVersion version)
    {
        if (item == NativeLootNames.Book && isEnchanted)
            return NativeLootNames.EnchantedBookItem;

        return NativeLootNames.ItemName(item, version)
            ?? throw new InvalidOperationException($"The native generator rolled unknown item {item}.");
    }
}

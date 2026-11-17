using System.Collections.Frozen;
using System.Globalization;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Loot;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.LootServ;

namespace Cubiomes_Viewer.Service.LootDescriptionServ;

public sealed class LootDescriptionService : ILootDescriptionService
{
    private const int TicksPerSecond = 20;
    private const int SecondsPerMinute = 60;
    private const string Separator = "  ·  ";
    private const string EmptyLabel = "Empty";

    private static readonly FrozenSet<string> _instantEffects = FrozenSet.Create(StringComparer.Ordinal, "minecraft:instant_health", "minecraft:instant_damage", "minecraft:saturation");

    private readonly ILootCatalog _catalog;

    public LootDescriptionService(ILootCatalog catalog)
    {
        _catalog = catalog;
    }

    public IReadOnlyList<ChestDescription> Describe(StructureLoot loot)
    {
        ArgumentNullException.ThrowIfNull(loot);

        var totals = loot.Containers.CountBy(container => container.Kind).ToDictionary();
        Dictionary<LootContainerKind, int> numbers = [];
        List<ChestDescription> described = [];
        foreach (var container in loot.Containers)
        {
            var number = numbers[container.Kind] = numbers.GetValueOrDefault(container.Kind) + 1;
            var name = totals[container.Kind] > 1 ? $"{GetKindName(container.Kind)} {number}" : GetKindName(container.Kind);
            described.Add(new ChestDescription(name + Separator + BlockPosText.ToBlock(container.Position), DescribeStacks(container.Stacks)));
        }

        return described;
    }

    public string? DescribeAccuracy(StructureType type, McVersion version) => type switch
    {
        StructureType.Mineshaft => "A few of these minecarts may be missing in the game.",
        StructureType.AbandonedCamp => "Where the containers stand is approximate.",
        StructureType.RuinedPortal or StructureType.RuinedPortalNether => "Where the chest stands is estimated, and its loot differs when it stands in a neighbouring chunk.",
        StructureType.Bastion => "Only the chests every bastion of this kind has are listed; most bastions have more.",
        StructureType.EndCity when version < McVersion.V1_16_1 => "Before 1.16.1 the loot of end cities may differ from the game.",
        _ => null,
    };

    /// <summary>A line per kind of item, with the stacks of the same item added up, in the order the items first appear.</summary>
    private string[] DescribeStacks(LootStack[] stacks)
    {
        if (stacks.Length == 0)
            return [EmptyLabel];

        Dictionary<string, int> counts = new(StringComparer.Ordinal);
        List<string> order = [];
        foreach (var stack in stacks)
        {
            var item = DescribeItem(stack);
            if (counts.TryAdd(item, stack.Count))
                order.Add(item);
            else
                counts[item] += stack.Count;
        }

        return [.. order.Select(item => counts[item] == 1 ? item : string.Create(CultureInfo.InvariantCulture, $"{counts[item]} × {item}"))];
    }

    private string DescribeItem(LootStack stack)
    {
        var name = _catalog.GetItemName(stack.Item);
        if (stack.Effect is { } effect)
            return $"{name} ({DescribeEffect(effect, stack.EffectTicks)})";

        return stack.Enchantments.Length == 0
            ? name
            : $"{name}: {string.Join(", ", stack.Enchantments.Select(enchantment => _catalog.GetEnchantmentName(enchantment.Enchantment, enchantment.Level)))}";
    }

    private string DescribeEffect(string effect, int ticks)
    {
        var name = _catalog.GetEffectName(effect);
        if (_instantEffects.Contains(effect) || ticks <= 0)
            return name;

        var seconds = ticks / TicksPerSecond;
        return string.Create(CultureInfo.InvariantCulture, $"{name}, {seconds / SecondsPerMinute}:{seconds % SecondsPerMinute:00}");
    }

    private static string GetKindName(LootContainerKind kind) => kind switch
    {
        LootContainerKind.Chest => "Chest",
        LootContainerKind.Barrel => "Barrel",
        LootContainerKind.Dispenser => "Dispenser",
        LootContainerKind.MinecartWithChest => "Minecart with chest",
        _ => throw new NotSupportedException($"Unsupported container kind: {kind}"),
    };
}

using Cubiomes_Viewer.Engine.Data.Loot;

namespace Cubiomes_Viewer.Engine.Tests.Service.LootServ;

/// <summary>Writes rolled loot as one line, so tests can compare loot rolled in different ways.</summary>
internal static class LootText
{
    public static string Describe(StructureLoot? loot)
        => loot is null ? "none" : $"{loot.IsKnown}: {string.Join(" | ", loot.Containers.Select(Describe))}";

    private static string Describe(LootContainer container)
        => $"{container.Kind} {container.Table} at {container.Position.X},{container.Position.Z}: {string.Join(", ", container.Stacks.Select(Describe))}";

    private static string Describe(LootStack stack)
        => $"{stack.Count} {stack.Item} [{string.Join(" ", stack.Enchantments.Select(enchantment => $"{enchantment.Enchantment}={enchantment.Level}"))}] {stack.Effect}/{stack.EffectTicks}";
}

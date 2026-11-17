namespace Cubiomes_Viewer.Engine.Data.Loot;

/// <summary>An item the containers of a structure type can hold, with whether they ever enchant it.</summary>
public sealed record LootItem(string Id, string Name, bool CanBeEnchanted);

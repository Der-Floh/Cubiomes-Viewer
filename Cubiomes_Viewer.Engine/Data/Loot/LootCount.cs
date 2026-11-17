namespace Cubiomes_Viewer.Engine.Data.Loot;

/// <summary>How many items of a <see cref="LootQuery"/> the containers of one structure hold, in the fullest one and all together.</summary>
public readonly record struct LootCount(int Containers, int MostInOne, int Total);

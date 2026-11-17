namespace Cubiomes_Viewer.Engine.Interop;

/// <summary>Mirrors <c>CV_LOOT_PLAIN</c> and <c>CV_LOOT_ENCHANTED</c>.</summary>
[Flags]
internal enum NativeLootKinds
{
    None = 0,
    Plain = 1,
    Enchanted = 2,
    Any = Plain | Enchanted,
}

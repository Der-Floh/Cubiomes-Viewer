using System.Runtime.InteropServices;

namespace Cubiomes_Viewer.Engine.Interop;

/// <summary>Mirrors <c>cv_loot_stack</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct NativeLootStack
{
    public const int MaxEnchantments = 16;
    public const int NoEffect = -1;

    public int Item;
    public int Count;
    public int Effect;
    public int EffectTicks;
    public int EnchantmentCount;
    public fixed int Enchantments[MaxEnchantments];
    public fixed int Levels[MaxEnchantments];
}

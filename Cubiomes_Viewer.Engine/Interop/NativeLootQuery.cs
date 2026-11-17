using System.Runtime.InteropServices;

namespace Cubiomes_Viewer.Engine.Interop;

/// <summary>Mirrors <c>cv_loot_query</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeLootQuery
{
    public int Item;
    public NativeLootKinds Kinds;
    public int Enchantment;
    public int MinLevel;
}

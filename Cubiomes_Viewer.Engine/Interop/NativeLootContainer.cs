using System.Runtime.InteropServices;

namespace Cubiomes_Viewer.Engine.Interop;

/// <summary>Mirrors <c>cv_loot_container</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeLootContainer
{
    public int Table;
    public int X;
    public int Z;
    public int FirstStack;
    public int StackCount;
}

using System.Runtime.InteropServices;

namespace Cubiomes_Viewer.Engine.Data.Worlds;

/// <summary>
/// A horizontal block position. Laid out as two consecutive 32-bit integers,
/// so the native generator can fill spans of positions directly.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct BlockPos(int X, int Z);

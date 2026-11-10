using System.Runtime.InteropServices;

namespace Cubiomes_Viewer.Engine.Interop;

/// <summary>Mirrors <c>cv_piece</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativePiece
{
    public int Type;
    public int Rotation;
    public int Depth;
    public int X0;
    public int Y0;
    public int Z0;
    public int X1;
    public int Y1;
    public int Z1;
}

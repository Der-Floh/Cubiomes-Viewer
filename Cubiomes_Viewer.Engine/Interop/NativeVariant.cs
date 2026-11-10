using System.Runtime.InteropServices;

namespace Cubiomes_Viewer.Engine.Interop;

/// <summary>Mirrors <c>cv_variant</c>.</summary>
[StructLayout(LayoutKind.Sequential)]
internal struct NativeVariant
{
    public int Abandoned;
    public int Giant;
    public int Underground;
    public int AirPocket;
    public int Basement;
    public int Cracked;
    public int Size;
    public int Start;
    public int Biome;
    public int Rotation;
    public int Mirror;
    public int X;
    public int Y;
    public int Z;
    public int SizeX;
    public int SizeY;
    public int SizeZ;
}

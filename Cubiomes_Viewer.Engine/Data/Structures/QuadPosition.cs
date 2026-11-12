using System.Runtime.InteropServices;

namespace Cubiomes_Viewer.Engine.Data.Structures;

/// <summary>
/// A quad whose four structures attempt to generate in the regions from (<see cref="RegionX"/>, <see cref="RegionZ"/>)
/// to the next region in both directions. Laid out as three 32-bit integers, as the native generator writes them.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly record struct QuadPosition(int RegionX, int RegionZ, QuadQuality Quality);

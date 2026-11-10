using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

using Cubiomes_Viewer.Engine.Data;

namespace Cubiomes_Viewer.Engine.Interop;

internal static unsafe partial class CubiomesNative
{
    private const string LibraryName = Constants.NativeLibraryName;

    [LibraryImport(LibraryName, EntryPoint = "cv_abi_version")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int AbiVersion();

    [LibraryImport(LibraryName, EntryPoint = "cv_mc_oldest")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int OldestVersion();

    [LibraryImport(LibraryName, EntryPoint = "cv_mc_newest")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int NewestVersion();

    [LibraryImport(LibraryName, EntryPoint = "cv_mc_name")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint VersionName(int version);

    [LibraryImport(LibraryName, EntryPoint = "cv_biome_name")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint BiomeName(int version, int biome);

    [LibraryImport(LibraryName, EntryPoint = "cv_default_biome_colors")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int DefaultBiomeColors(byte* rgb, int capacity);

    [LibraryImport(LibraryName, EntryPoint = "cv_world_create")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial nint WorldCreate(int version, uint flags);

    [LibraryImport(LibraryName, EntryPoint = "cv_world_destroy")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial void WorldDestroy(nint world);

    [LibraryImport(LibraryName, EntryPoint = "cv_world_apply")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int WorldApply(nint world, int dimension, ulong seed);

    [LibraryImport(LibraryName, EntryPoint = "cv_biome_at")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int BiomeAt(nint world, int scale, int x, int y, int z, int* biome);

    [LibraryImport(LibraryName, EntryPoint = "cv_gen_biomes_u8")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int GenerateBiomes(nint world, int scale, int x, int z, int sizeX, int sizeZ, int y, byte* destination, int capacity);

    [LibraryImport(LibraryName, EntryPoint = "cv_structure_pos")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int StructurePosition(int structureType, int version, ulong seed, int regionX, int regionZ, int* x, int* z);
}

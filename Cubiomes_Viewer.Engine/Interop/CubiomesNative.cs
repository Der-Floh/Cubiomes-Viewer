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

    [LibraryImport(LibraryName, EntryPoint = "cv_biome_dimension")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int BiomeDimension(int version, int biome, int* dimension);

    [LibraryImport(LibraryName, EntryPoint = "cv_quad_supported")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int QuadSupported(int structureType, int version);

    [LibraryImport(LibraryName, EntryPoint = "cv_quad_hut_low_bits")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int QuadHutLowBits(int quality, ulong* values, int capacity);

    [LibraryImport(LibraryName, EntryPoint = "cv_quad_hut_scan")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int QuadHutScan(ulong low20, uint firstHigh, uint count, ulong* bases, int capacity);

    [LibraryImport(LibraryName, EntryPoint = "cv_quad_hut_base_info")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int QuadHutBaseInfo(ulong baseSeed, float* radius);

    [LibraryImport(LibraryName, EntryPoint = "cv_quad_seed_of_base")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int QuadSeedOfBase(NativeRules* rules, int structureType, int version, ulong baseSeed, int regionX, int regionZ, ulong* seed);

    [LibraryImport(LibraryName, EntryPoint = "cv_quads_in_area")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int QuadsInArea(NativeRules* rules, int structureType, int version, ulong seed, int regionX0, int regionZ0, int regionX1, int regionZ1, int* quads, int capacity);

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

    [LibraryImport(LibraryName, EntryPoint = "cv_world_set_rules")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int WorldSetRules(nint world, NativeRules* rules);

    [LibraryImport(LibraryName, EntryPoint = "cv_biome_at")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int BiomeAt(nint world, int scale, int x, int y, int z, int* biome);

    [LibraryImport(LibraryName, EntryPoint = "cv_gen_biomes_u8")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int GenerateBiomes(nint world, int scale, int x, int z, int sizeX, int sizeZ, int y, byte* destination, int capacity);

    [LibraryImport(LibraryName, EntryPoint = "cv_climate_supported")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int ClimateSupported(int version, int dimension, int parameter);

    [LibraryImport(LibraryName, EntryPoint = "cv_gen_climate")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int GenerateClimate(nint world, int parameter, int octaves, int scale, int x, int z, int sizeX, int sizeZ, int y, short* destination, int capacity);

    [LibraryImport(LibraryName, EntryPoint = "cv_layer_supported")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int LayerSupported(int version, int dimension, int layer, int scale);

    [LibraryImport(LibraryName, EntryPoint = "cv_gen_layer_u8")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int GenerateLayer(nint world, int layer, int scale, int x, int z, int sizeX, int sizeZ, byte* destination, int capacity);

    [LibraryImport(LibraryName, EntryPoint = "cv_height_supported")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int HeightSupported(int version, int dimension);

    [LibraryImport(LibraryName, EntryPoint = "cv_gen_heights")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int GenerateHeights(nint world, int scale, int x, int z, int sizeX, int sizeZ, short* destination, int capacity);

    [LibraryImport(LibraryName, EntryPoint = "cv_structure_config")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int StructureConfig(int structureType, int version, int* dimension, int* regionBlocks);

    [LibraryImport(LibraryName, EntryPoint = "cv_structure_salt")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int StructureSalt(int structureType, int version, int* salt);

    [LibraryImport(LibraryName, EntryPoint = "cv_structure_pos")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int StructurePosition(NativeRules* rules, int structureType, int version, ulong seed, int regionX, int regionZ, int* x, int* z);

    [LibraryImport(LibraryName, EntryPoint = "cv_structure_viable")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int StructureViable(nint world, int structureType, int x, int z);

    [LibraryImport(LibraryName, EntryPoint = "cv_structure_biome_viable")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int StructureBiomeViable(int structureType, int version, int biome);

    [LibraryImport(LibraryName, EntryPoint = "cv_structures_in_area")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int StructuresInArea(nint world, int structureType, int x0, int z0, int x1, int z1, int* positions, int capacity);

    [LibraryImport(LibraryName, EntryPoint = "cv_structure_attempts_in_area")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int StructureAttemptsInArea(NativeRules* rules, int structureType, int version, ulong seed, int x0, int z0, int x1, int z1, int* positions, int capacity);

    [LibraryImport(LibraryName, EntryPoint = "cv_structure_variant")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int StructureVariant(nint world, int structureType, int x, int z, NativeVariant* variant);

    [LibraryImport(LibraryName, EntryPoint = "cv_structure_pieces")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int StructurePieces(nint world, int structureType, int x, int z, NativePiece* pieces, int capacity);

    [LibraryImport(LibraryName, EntryPoint = "cv_end_gateways")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int EndGateways(nint world, int* positions, int capacity);

    [LibraryImport(LibraryName, EntryPoint = "cv_spawn")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int Spawn(nint world, int* x, int* z);

    [LibraryImport(LibraryName, EntryPoint = "cv_strongholds")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int Strongholds(nint world, int* positions, int capacity);

    [LibraryImport(LibraryName, EntryPoint = "cv_slime_chunks")]
    [UnmanagedCallConv(CallConvs = [typeof(CallConvCdecl)])]
    internal static partial int SlimeChunks(ulong seed, int chunkX, int chunkZ, int sizeX, int sizeZ, byte* destination, int capacity);
}

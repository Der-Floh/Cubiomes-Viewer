using System.Collections.Concurrent;
using System.Globalization;
using System.Runtime.InteropServices;

using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;

namespace Cubiomes_Viewer.Engine.Service.WorldGenServ;

public sealed class BiomeCatalog : IBiomeCatalog
{
    private const char ResourceNameSeparator = '_';

    public BiomePalette DefaultPalette => field ??= LoadDefaultPalette();

    private readonly ConcurrentDictionary<(McVersion Version, Dimension Dimension), int[]> _biomes = [];

    public string? GetName(McVersion version, int biome)
        => Marshal.PtrToStringUTF8(CubiomesNative.BiomeName((int)version, biome));

    public string? GetDisplayName(McVersion version, int biome)
        => GetName(version, biome) is { } name
            ? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.Replace(ResourceNameSeparator, ' '))
            : null;

    public unsafe bool Generates(McVersion version, Dimension dimension, int biome)
    {
        if (biome is < 0 or > BiomeSet.MaxBiome)
            return false;

        int biomeDimension;
        var generates = CubiomesNativeException.ThrowIfError(CubiomesNative.BiomeDimension((int)version, biome, &biomeDimension), nameof(Generates));
        return generates > 0 && biomeDimension == (int)dimension;
    }

    public IReadOnlyList<int> GetBiomes(McVersion version, Dimension dimension)
        => _biomes.GetOrAdd((version, dimension), key => [.. Enumerable.Range(0, BiomeSet.MaxBiome + 1).Where(biome => Generates(key.Version, key.Dimension, biome))]);

    private static unsafe BiomePalette LoadDefaultPalette()
    {
        var rgb = new byte[Constants.BiomeColorCount * Constants.BiomeColorChannels];
        fixed (byte* buffer = rgb)
            CubiomesNativeException.ThrowIfError(CubiomesNative.DefaultBiomeColors(buffer, rgb.Length), nameof(LoadDefaultPalette));

        return BiomePalette.FromRgb(rgb);
    }
}

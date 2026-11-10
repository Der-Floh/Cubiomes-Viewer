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

    public string? GetName(McVersion version, int biome)
        => Marshal.PtrToStringUTF8(CubiomesNative.BiomeName((int)version, biome));

    public string? GetDisplayName(McVersion version, int biome)
        => GetName(version, biome) is { } name
            ? CultureInfo.InvariantCulture.TextInfo.ToTitleCase(name.Replace(ResourceNameSeparator, ' '))
            : null;

    private static unsafe BiomePalette LoadDefaultPalette()
    {
        var rgb = new byte[Constants.BiomeColorCount * Constants.BiomeColorChannels];
        fixed (byte* buffer = rgb)
            CubiomesNativeException.ThrowIfError(CubiomesNative.DefaultBiomeColors(buffer, rgb.Length), nameof(LoadDefaultPalette));

        return BiomePalette.FromRgb(rgb);
    }
}

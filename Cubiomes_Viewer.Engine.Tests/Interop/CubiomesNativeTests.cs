using System.Runtime.InteropServices;

using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;

namespace Cubiomes_Viewer.Engine.Tests.Interop;

public sealed class CubiomesNativeTests
{
    private static readonly Dictionary<StructureType, string?> _nativeStructureNames = new()
    {
        [StructureType.Feature] = null,
        [StructureType.DesertPyramid] = "desert_pyramid",
        [StructureType.JungleTemple] = "jungle_pyramid",
        [StructureType.SwampHut] = "swamp_hut",
        [StructureType.Igloo] = "igloo",
        [StructureType.Village] = "village",
        [StructureType.OceanRuin] = "ocean_ruin",
        [StructureType.Shipwreck] = "shipwreck",
        [StructureType.Monument] = "monument",
        [StructureType.Mansion] = "mansion",
        [StructureType.Outpost] = "pillager_outpost",
        [StructureType.RuinedPortal] = "ruined_portal",
        [StructureType.RuinedPortalNether] = "ruined_portal_nether",
        [StructureType.AncientCity] = "ancient_city",
        [StructureType.Treasure] = "buried_treasure",
        [StructureType.Mineshaft] = "mineshaft",
        [StructureType.DesertWell] = "desert_well",
        [StructureType.Geode] = "amethyst_geode",
        [StructureType.Fortress] = "fortress",
        [StructureType.Bastion] = "bastion_remnant",
        [StructureType.NetherFossil] = "nether_fossil",
        [StructureType.EndCity] = "end_city",
        [StructureType.EndGateway] = "end_gateway",
        [StructureType.EndIsland] = null,
        [StructureType.TrailRuins] = "trail_ruins",
        [StructureType.TrialChambers] = "trial_chambers",
        [StructureType.AbandonedCamp] = "abandoned_camp",
    };

    [Fact]
    public void AbiVersion_LoadedLibrary_MatchesManagedConstant()
        => Assert.Equal(Constants.NativeAbiVersion, CubiomesNative.AbiVersion());

    public static TheoryData<StructureType> ManagedStructureTypes() => [.. Enum.GetValues<StructureType>()];

    [Theory]
    [MemberData(nameof(ManagedStructureTypes))]
    public void StructureName_ManagedType_IsTheNativeTypeOfTheSameNumber(StructureType type)
        => Assert.Equal(_nativeStructureNames[type], Marshal.PtrToStringUTF8(CubiomesNative.StructureName((int)type)));

    [Fact]
    public unsafe void DefaultBiomeColors_FullBuffer_FillsEveryChannel()
    {
        var colors = new byte[Constants.BiomeColorCount * Constants.BiomeColorChannels];

        int written;
        fixed (byte* buffer = colors)
            written = CubiomesNative.DefaultBiomeColors(buffer, colors.Length);

        Assert.Equal(colors.Length, written);
        Assert.Contains(colors, channel => channel != 0);
    }

    [Fact]
    public unsafe void DefaultBiomeColors_BufferTooSmall_ReportsInsufficientCapacity()
    {
        var colors = new byte[Constants.BiomeColorChannels];

        int result;
        fixed (byte* buffer = colors)
            result = CubiomesNative.DefaultBiomeColors(buffer, colors.Length);

        Assert.Equal((int)NativeStatus.InsufficientCapacity, result);
    }
}

using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.Engine.Tests.Service.StructureServ;

public sealed class CubiomesStructureFinderTests
{
    private const long Seed = 123456789;
    private const long UpperBitsOffset = 1L << 48;

    private readonly CubiomesStructureFinder _finder = new();

    public static TheoryData<StructureType, McVersion> AllTypesAndVersions()
    {
        var data = new TheoryData<StructureType, McVersion>();
        foreach (var type in Enum.GetValues<StructureType>())
            foreach (var version in Enum.GetValues<McVersion>().Where(version => version != McVersion.Undefined))
                data.Add(type, version);
        return data;
    }

    [Theory]
    [MemberData(nameof(AllTypesAndVersions))]
    public void TryGetRegionPosition_AnyTypeAndVersion_IsDeterministic(StructureType type, McVersion version)
    {
        var first = _finder.TryGetRegionPosition(type, version, StructureRules.Default, Seed, 3, -2, out var firstPosition);
        var second = _finder.TryGetRegionPosition(type, version, StructureRules.Default, Seed, 3, -2, out var secondPosition);

        Assert.Equal(first, second);
        Assert.Equal(firstPosition, secondPosition);
    }

    [Fact]
    public void TryGetRegionPosition_Village_IgnoresUpperSeedBits()
    {
        var found = _finder.TryGetRegionPosition(StructureType.Village, McVersion.V1_21_3, StructureRules.Default, Seed, 1, 1, out var position);
        var foundWithUpperBits = _finder.TryGetRegionPosition(StructureType.Village, McVersion.V1_21_3, StructureRules.Default, Seed + UpperBitsOffset, 1, 1, out var positionWithUpperBits);

        Assert.True(found);
        Assert.True(foundWithUpperBits);
        Assert.Equal(position, positionWithUpperBits);
    }

    [Fact]
    public void TryGetRegionPosition_StructureNotInVersion_ReturnsFalse()
        => Assert.False(_finder.TryGetRegionPosition(StructureType.TrialChambers, McVersion.V1_12_2, StructureRules.Default, Seed, 0, 0, out _));

    [Fact]
    public void TryGetRegionPosition_UnknownStructureType_ThrowsInvalidArgument()
    {
        var exception = Assert.Throws<CubiomesNativeException>(() => _finder.TryGetRegionPosition((StructureType)999, McVersion.V1_21_3, StructureRules.Default, Seed, 0, 0, out _));

        Assert.Equal(NativeStatus.InvalidArgument, exception.Status);
    }
}

using System.Diagnostics;

using Cubiomes_Viewer.Engine.Data;
using Cubiomes_Viewer.Engine.Data.SelfTests;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Service.SelfTestServ;

public sealed class SelfTestService : ISelfTestService
{
    private const long AnchorSeed = 262;
    private const int AnchorSeaLevel = 63;
    private const int MushroomFieldsBiomeId = 14;
    private const int UnsupportedScale = 3;
    private const int AreaScale = 4;
    private const int AreaSize = 24;
    private const int AreaOrigin = -40;
    private const int AreaHeight = 16;
    private const long AreaSeed = 8675309;
    private const int VillageRegionBlocks = 34 * 16;
    private const long VillageSeed = 123456789;

    private readonly IWorldGeneratorFactory _generatorFactory;
    private readonly IVersionCatalog _versionCatalog;
    private readonly IStructureFinder _structureFinder;
    private readonly ISlimeChunkFinder _slimeChunkFinder;
    private readonly ILogger<SelfTestService> _logger;

    public SelfTestService(IWorldGeneratorFactory generatorFactory, IVersionCatalog versionCatalog, IStructureFinder structureFinder, ISlimeChunkFinder slimeChunkFinder, ILogger<SelfTestService> logger)
    {
        _generatorFactory = generatorFactory;
        _versionCatalog = versionCatalog;
        _structureFinder = structureFinder;
        _slimeChunkFinder = slimeChunkFinder;
        _logger = logger;
    }

    public SelfTestReport Run()
    {
        var stopwatch = Stopwatch.StartNew();
        List<SelfTestCheck> checks =
        [
            RunCheck("Native ABI version", CheckAbiVersion),
            RunCheck("Version catalog matches the native library", CheckVersionCatalog),
            RunCheck("Seed 262 has mushroom fields at the origin", CheckAnchorSeed),
            .. ReferenceBiomeHash.QuickOverworldHashes.Select(expected =>
                RunCheck($"Biome regression hash {expected.Version}", () => CheckBiomeHash(expected.Version, expected.Hash))),
            RunCheck("Area generation matches point sampling (1.17 layers)", () => CheckAreaMatchesPoints(McVersion.V1_17_1)),
            RunCheck("Area generation matches point sampling (newest noise)", () => CheckAreaMatchesPoints(_versionCatalog.Newest)),
            RunCheck("Village positions stay inside their region", CheckVillageRegionPositions),
            .. ReferenceStructureHash.StructureCases.Select(expected =>
                RunCheck($"Structure regression hash {expected.Name}", () => CheckStructureHash(expected))),
            .. ReferenceStructureHash.StrongholdHashes.Select(expected =>
                RunCheck($"Stronghold regression hash {expected.Version}", () => CheckStrongholdHash(expected.Version, expected.Hash))),
            .. ReferenceStructureHash.Spawns.Select(expected =>
                RunCheck($"Spawn point {expected.Version}", () => CheckSpawn(expected.Version, expected.Spawn))),
            RunCheck("Slime chunk regression hash", CheckSlimeChunkHash),
            RunCheck("Invalid input is rejected without terminating", CheckInvalidInputIsRejected),
        ];

        var report = new SelfTestReport { Checks = checks, Duration = stopwatch.Elapsed };
        _logger.LogInformation("Self-test finished: {Report}", report);
        foreach (var failed in checks.Where(check => !check.Passed))
            _logger.LogWarning("Self-test check failed: {Check}", failed);

        return report;
    }

    private static SelfTestCheck RunCheck(string name, Func<(bool Passed, string Detail)> check)
    {
        try
        {
            var (passed, detail) = check();
            return new SelfTestCheck { Name = name, Passed = passed, Detail = detail };
        }
        catch (Exception ex) when (ex is CubiomesNativeException or DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or ArgumentException)
        {
            return new SelfTestCheck { Name = name, Passed = false, Detail = $"{ex.GetType().Name}: {ex.Message}" };
        }
    }

    private static (bool, string) CheckAbiVersion()
    {
        var actual = CubiomesNative.AbiVersion();
        return (actual == Constants.NativeAbiVersion, $"expected {Constants.NativeAbiVersion}, got {actual}");
    }

    private (bool, string) CheckVersionCatalog()
    {
        var expected = Enum.GetValues<McVersion>().Where(version => version != McVersion.Undefined).ToArray();
        var names = _versionCatalog.Versions.Select(_versionCatalog.GetName).ToArray();
        var matches = _versionCatalog.Versions.SequenceEqual(expected) && names.All(name => name.Length > 0);
        return (matches, $"{_versionCatalog.Versions.Count} native versions, {expected.Length} managed versions, newest {_versionCatalog.GetName(_versionCatalog.Newest)}");
    }

    private (bool, string) CheckAnchorSeed()
    {
        using var generator = _generatorFactory.Create(McVersion.V1_18_2, GeneratorFlags.None);
        generator.Apply(Dimension.Overworld, AnchorSeed);
        var biome = generator.GetBiomeAt(scale: 1, x: 0, y: AnchorSeaLevel, z: 0);
        return (biome == MushroomFieldsBiomeId, $"expected biome {MushroomFieldsBiomeId}, got {biome}");
    }

    private (bool, string) CheckBiomeHash(McVersion version, uint expected)
    {
        using var generator = _generatorFactory.Create(version, GeneratorFlags.None);
        var actual = ReferenceBiomeHash.Compute(generator, Dimension.Overworld, ReferenceBiomeHash.QuickBits);
        return (actual == expected, $"expected {expected:x8}, got {actual:x8}");
    }

    private (bool, string) CheckAreaMatchesPoints(McVersion version)
    {
        using var generator = _generatorFactory.Create(version, GeneratorFlags.None);
        generator.Apply(Dimension.Overworld, AreaSeed);

        var area = new BiomeArea(AreaScale, AreaOrigin, AreaOrigin, AreaSize, AreaSize, AreaHeight);
        var tile = new byte[area.SampleCount];
        generator.GenerateBiomes(area, tile);

        var mismatches = 0;
        for (var z = 0; z < area.SizeZ; z++)
        {
            for (var x = 0; x < area.SizeX; x++)
            {
                var point = generator.GetBiomeAt(area.Scale, area.X + x, area.Y, area.Z + z);
                if (tile[z * area.SizeX + x] != ToTileValue(point))
                    mismatches++;
            }
        }

        return (mismatches == 0, $"{mismatches} of {area.SampleCount} samples differ");
    }

    private (bool, string) CheckVillageRegionPositions()
    {
        var outside = 0;
        var found = 0;
        for (var regionZ = -2; regionZ <= 2; regionZ++)
        {
            for (var regionX = -2; regionX <= 2; regionX++)
            {
                if (!_structureFinder.TryGetRegionPosition(StructureType.Village, _versionCatalog.Newest, VillageSeed, regionX, regionZ, out var position))
                    continue;

                found++;
                if (!IsInsideRegion(position.X, regionX) || !IsInsideRegion(position.Z, regionZ))
                    outside++;
            }
        }

        return (found > 0 && outside == 0, $"{found} positions, {outside} outside their region");
    }

    private (bool, string) CheckStructureHash(ReferenceStructureHash.StructureCase expected)
    {
        using var generator = _generatorFactory.Create(expected.Version, GeneratorFlags.None);
        generator.Apply(expected.Dimension, ReferenceStructureHash.Seed);

        var count = 0;
        var actual = ReferenceStructureHash.Hash([]);
        foreach (var type in expected.Types)
        {
            var positions = generator.FindStructures(type, expected.Area);
            actual = ReferenceStructureHash.Hash(positions, actual);
            count += positions.Length;
        }

        return (actual == expected.Hash, $"expected {expected.Hash:x8}, got {actual:x8} from {count} structures");
    }

    private (bool, string) CheckStrongholdHash(McVersion version, uint expected)
    {
        using var generator = _generatorFactory.Create(version, GeneratorFlags.None);
        generator.Apply(Dimension.Overworld, ReferenceStructureHash.Seed);

        var strongholds = generator.FindStrongholds();
        var actual = ReferenceStructureHash.Hash(strongholds);
        return (actual == expected, $"expected {expected:x8}, got {actual:x8} from {strongholds.Length} strongholds");
    }

    private (bool, string) CheckSpawn(McVersion version, BlockPos expected)
    {
        using var generator = _generatorFactory.Create(version, GeneratorFlags.None);
        generator.Apply(Dimension.Overworld, ReferenceStructureHash.Seed);

        var actual = generator.FindSpawn();
        return (actual == expected, $"expected {expected}, got {actual}");
    }

    private (bool, string) CheckSlimeChunkHash()
    {
        const int side = ReferenceStructureHash.SlimeChunkSide;
        var chunks = new byte[side * side];
        _slimeChunkFinder.FindSlimeChunks(ReferenceStructureHash.Seed, ReferenceStructureHash.SlimeChunkOrigin, ReferenceStructureHash.SlimeChunkOrigin, side, side, chunks);

        var actual = ReferenceStructureHash.Hash(chunks);
        return (actual == ReferenceStructureHash.SlimeChunkHash, $"expected {ReferenceStructureHash.SlimeChunkHash:x8}, got {actual:x8}");
    }

    private (bool, string) CheckInvalidInputIsRejected()
    {
        using var generator = _generatorFactory.Create(_versionCatalog.Newest, GeneratorFlags.None);
        generator.Apply(Dimension.Overworld, AnchorSeed);

        try
        {
            generator.GetBiomeAt(UnsupportedScale, 0, 0, 0);
        }
        catch (CubiomesNativeException ex) when (ex.Status == NativeStatus.InvalidArgument)
        {
            return (true, "an unsupported scale was rejected");
        }

        return (false, "an unsupported scale was accepted");
    }

    private static bool IsInsideRegion(int coordinate, int region)
        => coordinate >= region * VillageRegionBlocks && coordinate < (region + 1) * VillageRegionBlocks;

    private static byte ToTileValue(int biome)
        => biome is < 0 or >= Constants.NoBiomeTileValue ? Constants.NoBiomeTileValue : (byte)biome;
}

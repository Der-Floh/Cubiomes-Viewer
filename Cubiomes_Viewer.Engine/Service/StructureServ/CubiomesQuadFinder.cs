using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Interop;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class CubiomesQuadFinder : IQuadFinder
{
    private const int MaxLowBits = 64;

    private readonly Dictionary<QuadQuality, ulong[]> _lowBits = [];
    private readonly object _gate = new();

    public bool SupportsQuads(StructureType type, McVersion version)
        => CubiomesNativeException.ThrowIfError(CubiomesNative.QuadSupported((int)type, (int)version), nameof(SupportsQuads)) > 0;

    public IReadOnlyList<ulong> GetLowBits(QuadQuality quality)
    {
        lock (_gate)
        {
            if (!_lowBits.TryGetValue(quality, out var values))
            {
                values = LoadLowBits(quality);
                _lowBits.Add(quality, values);
            }

            return values;
        }
    }

    public unsafe int ScanQuadHutBases(ulong lowBits, uint firstHigh, uint count, Span<ulong> destination)
    {
        fixed (ulong* bases = destination)
            return CubiomesNativeException.ThrowIfError(CubiomesNative.QuadHutScan(lowBits, firstHigh, count, bases, destination.Length), nameof(ScanQuadHutBases));
    }

    public unsafe QuadHutBase? DescribeQuadHutBase(ulong value)
    {
        float radius;
        var quality = CubiomesNativeException.ThrowIfError(CubiomesNative.QuadHutBaseInfo(value, &radius), nameof(DescribeQuadHutBase));
        return quality == 0 ? null : new QuadHutBase(value, (QuadQuality)quality, radius);
    }

    public unsafe long GetSeedOfBase(StructureType type, McVersion version, StructureRules rules, ulong value, int regionX, int regionZ)
    {
        var native = NativeRules.From(rules);
        ulong seed;
        CubiomesNativeException.ThrowIfError(CubiomesNative.QuadSeedOfBase(&native, (int)type, (int)version, value, regionX, regionZ, &seed), nameof(GetSeedOfBase));
        return (long)seed;
    }

    public unsafe int FindQuads(StructureType type, McVersion version, StructureRules rules, long seed, int regionX0, int regionZ0, int regionX1, int regionZ1, Span<QuadPosition> destination)
    {
        var native = NativeRules.From(rules);
        fixed (QuadPosition* quads = destination)
        {
            var count = CubiomesNative.QuadsInArea(&native, (int)type, (int)version, unchecked((ulong)seed), regionX0, regionZ0, regionX1, regionZ1, (int*)quads, destination.Length);
            return CubiomesNativeException.ThrowIfError(count, nameof(FindQuads));
        }
    }

    private static unsafe ulong[] LoadLowBits(QuadQuality quality)
    {
        var buffer = stackalloc ulong[MaxLowBits];
        var count = CubiomesNativeException.ThrowIfError(CubiomesNative.QuadHutLowBits((int)quality, buffer, MaxLowBits), nameof(GetLowBits));
        return new ReadOnlySpan<ulong>(buffer, count).ToArray();
    }
}

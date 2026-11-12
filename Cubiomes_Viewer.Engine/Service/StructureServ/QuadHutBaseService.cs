using System.Globalization;
using System.Runtime.InteropServices;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

using Microsoft.Extensions.Logging;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class QuadHutBaseService : IQuadHutBaseService, IDisposable
{
    private const int HighBits = 28;
    private const int ChunkBits = 22;
    private const uint ChunkSize = 1u << ChunkBits;
    private const int ChunksPerLowBits = 1 << (HighBits - ChunkBits);
    private const int StorageFormat = 1;
    private const string StorageKeyFormat = "quad-hut-bases-{0}-{1:x5}.bin";
    private static readonly JobPriority _priority = new(JobPriorityClass.Visible);
    private static readonly QuadQuality[] _classes = [QuadQuality.Ideal, QuadQuality.Classic, QuadQuality.Normal, QuadQuality.Barely];

    private readonly IComputeService _compute;
    private readonly IQuadFinder _finder;
    private readonly IAppDataStore _store;
    private readonly ILogger<QuadHutBaseService> _logger;
    private readonly Dictionary<ulong, ulong[]> _bases = [];
    private readonly SemaphoreSlim _gate = new(1, 1);

    public QuadHutBaseService(IComputeService compute, IQuadFinder finder, IAppDataStore store, ILogger<QuadHutBaseService> logger)
    {
        _compute = compute;
        _finder = finder;
        _store = store;
        _logger = logger;
    }

    public async Task<IReadOnlyList<ulong>> GetBasesAsync(QuadQuality worst, IProgress<double>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!_classes.Contains(worst))
            throw new ArgumentOutOfRangeException(nameof(worst), worst, "Quad-hut bases come in the classes ideal, classic, normal and barely.");

        var lowBits = GetLowBitsUpTo(worst);
        await _gate.WaitAsync(cancellationToken);
        try
        {
            await LoadAsync(lowBits, progress, cancellationToken);
        }
        finally
        {
            _gate.Release();
        }

        return [.. lowBits.SelectMany(low => _bases[low])];
    }

    public void Dispose() => _gate.Dispose();

    /// <summary>The lower bits of each class, without those a better class already has, best class first.</summary>
    private List<ulong> GetLowBitsUpTo(QuadQuality worst)
    {
        List<ulong> lowBits = [];
        foreach (var quality in _classes.Where(quality => quality <= worst))
            lowBits.AddRange(_finder.GetLowBits(quality).Where(low => !lowBits.Contains(low)));
        return lowBits;
    }

    private async Task LoadAsync(List<ulong> lowBits, IProgress<double>? progress, CancellationToken cancellationToken)
    {
        List<ulong> missing = [];
        foreach (var low in lowBits.Where(low => !_bases.ContainsKey(low)))
        {
            if (await ReadStoredAsync(low, cancellationToken) is { } stored)
                _bases[low] = stored;
            else
                missing.Add(low);
        }

        for (var index = 0; index < missing.Count; index++)
        {
            var low = missing[index];
            var done = index;
            var bases = await ScanAsync(low, share => progress?.Report((done + share) / missing.Count), cancellationToken);
            _bases[low] = bases;
            await StoreAsync(low, bases, cancellationToken);
        }

        if (missing.Count > 0)
            progress?.Report(1);
    }

    private async Task<ulong[]> ScanAsync(ulong low, Action<double> reportShare, CancellationToken cancellationToken)
    {
        var finished = 0;
        var scans = Enumerable.Range(0, ChunksPerLowBits).Select(async chunk =>
        {
            var scan = await _compute.RunAsync(new QuadHutBaseScanJob(low, (uint)chunk * ChunkSize, ChunkSize), _priority, cancellationToken);
            reportShare((double)Interlocked.Increment(ref finished) / ChunksPerLowBits);
            return scan;
        });

        var results = await Task.WhenAll(scans);
        var bases = results.SelectMany(result => result.Bases).ToArray();
        _logger.LogInformation("Found {Count} quad-hut bases with the lower bits {LowBits:x5}", bases.Length, low);
        return bases;
    }

    private async Task<ulong[]?> ReadStoredAsync(ulong low, CancellationToken cancellationToken)
    {
        var content = await _store.ReadAsync(GetStorageKey(low), cancellationToken);
        if (content is null)
            return null;
        if (content.Length % sizeof(ulong) == 0)
            return MemoryMarshal.Cast<byte, ulong>(content).ToArray();

        _logger.LogWarning("The stored quad-hut bases with the lower bits {LowBits:x5} are unreadable; finding them again", low);
        return null;
    }

    private Task StoreAsync(ulong low, ulong[] bases, CancellationToken cancellationToken)
        => _store.WriteAsync(GetStorageKey(low), MemoryMarshal.AsBytes(bases.AsSpan()).ToArray(), cancellationToken);

    private static string GetStorageKey(ulong low) => string.Format(CultureInfo.InvariantCulture, StorageKeyFormat, StorageFormat, low);
}

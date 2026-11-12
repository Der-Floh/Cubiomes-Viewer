using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.AnalysisServ;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.StorageServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>How much of the area each biome covers in the dimension the map shows, and where its nearest sample is.</summary>
public sealed partial class BiomeAnalysisViewModel : AnalysisTabViewModel
{
    private const int DefaultScale = 16;

    public override string Title { get; } = "Biomes";
    public override string RunLabel { get; } = "Count biomes";
    public string ScaleLabel { get; } = "Sample every";
    public string HeightLabel { get; } = "Sample at Y";
    public string Hint { get; } = "Counts the samples of each biome in the area, in the dimension the map shows. With one seed each line also tells where the biome's sample nearest to the centre of the area is.";
    public decimal MinHeight { get; } = Constants.MinSampleHeight;
    public decimal MaxHeight { get; } = Constants.MaxSampleHeight;

    [ObservableProperty]
    public partial ComboChoice<int>? SelectedScale { get; set; }

    [ObservableProperty]
    public partial decimal? Height { get; set; } = ViewSettings.DefaultSampleHeight;

    public ObservableCollection<ComboChoice<int>> Scales { get; } =
    [
        new() { Display = "Block", Value = 1 },
        new() { Display = "4 blocks", Value = 4 },
        new() { Display = "16 blocks", Value = 16 },
        new() { Display = "64 blocks", Value = 64 },
        new() { Display = "256 blocks", Value = 256 },
    ];

    protected override string FileName { get; } = "biomes.csv";

    private readonly IAnalysisService _analysis;
    private readonly IBiomeCatalog _biomeCatalog;
    private readonly WorldSession _session;

    public BiomeAnalysisViewModel(AnalysisScopeViewModel scope, IAnalysisService analysis, IBiomeCatalog biomeCatalog, WorldSession session, MapViewModel map, ISeedHistoryService seedHistory, IStorageService storage, ISettingsService settingsService, INotificationService notifications)
        : base(scope, session, map, seedHistory, storage, settingsService, notifications)
    {
        _analysis = analysis;
        _biomeCatalog = biomeCatalog;
        _session = session;
        SelectedScale = Scales.First(choice => choice.Value == DefaultScale);
    }

    protected override async Task<AnalysisResult> AnalyseAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        var worlds = Scope.GetWorlds(_session.Dimension);
        BiomeCensusRequest[] requests = [.. worlds.Select(world => new BiomeCensusRequest(world, Scope.Area, Scope.Reference, SelectedScale?.Value ?? DefaultScale, (int)Math.Round(Height ?? ViewSettings.DefaultSampleHeight)))];
        if (_analysis.FindBiomeProblems(requests[0]) is [var problem, ..])
            throw new InvalidOperationException(problem);

        var census = await _analysis.CountBiomesAsync(requests, progress, cancellationToken);
        return new AnalysisResult(CreateRows(worlds, census), CreateTable(worlds, census), DescribeTotal(worlds, census));
    }

    /// <summary>One line per biome over all worlds, the biome that covers most first.</summary>
    private List<AnalysisRow> CreateRows(IReadOnlyList<WorldKey> worlds, IReadOnlyList<BiomeCensus> census)
    {
        var total = census.Sum(result => (long)result.Samples);
        return [.. census
            .SelectMany((result, index) => result.Tallies.Select(tally => (World: worlds[index], Tally: tally)))
            .GroupBy(entry => entry.Tally.Biome)
            .Select(group => CreateRow(group.Key, [.. group], total, worlds.Count))
            .OrderByDescending(row => row.Samples)
            .ThenBy(row => row.Row.Title, StringComparer.Ordinal)
            .Select(row => row.Row)];
    }

    private (AnalysisRow Row, long Samples) CreateRow(int biome, IReadOnlyList<(WorldKey World, BiomeTally Tally)> entries, long total, int worldCount)
    {
        var samples = entries.Sum(entry => (long)entry.Tally.Samples);
        var share = total > 0 ? (double)samples / total : 0;
        var (world, nearest) = entries.MinBy(entry => entry.Tally.NearestSquaredDistance);
        var detail = worldCount == 1
            ? Format($"{share:P1} · {samples:N0} samples · nearest {DescribePosition(nearest.Nearest, Scope.Reference)}")
            : Format($"{share:P1} of all samples · in {entries.Count:N0} of {worldCount:N0} seeds");
        return (new AnalysisRow(GetName(world.Version, biome), detail, world, nearest.Nearest), samples);
    }

    private CsvTable CreateTable(IReadOnlyList<WorldKey> worlds, IReadOnlyList<BiomeCensus> census)
    {
        var table = new CsvTable("Seed", "Biome", "Samples", "Share", "Nearest X", "Nearest Z", "Distance");
        for (var index = 0; index < worlds.Count; index++)
        {
            foreach (var tally in census[index].Tallies)
            {
                var share = census[index].Samples > 0 ? (double)tally.Samples / census[index].Samples : 0;
                table.Add(Format($"{worlds[index].Seed}"), GetName(worlds[index].Version, tally.Biome), Format($"{tally.Samples}"), Format($"{share:0.######}"), Format($"{tally.Nearest.X}"), Format($"{tally.Nearest.Z}"), Format($"{tally.NearestDistance:0.#}"));
            }
        }

        return table;
    }

    private static string DescribeTotal(IReadOnlyList<WorldKey> worlds, IReadOnlyList<BiomeCensus> census)
        => Format($"Counted {census.Sum(result => (long)result.Samples):N0} samples in {SearchProgressText.Count(worlds.Count, "seed")}.");

    private string GetName(McVersion version, int biome) => _biomeCatalog.GetDisplayName(version, biome) ?? Format($"Biome {biome}");
}

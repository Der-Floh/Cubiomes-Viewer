using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Which biomes a biome condition looks for, where and how it samples them: on a grid, or at random points for a share of the area.</summary>
public sealed partial class BiomeFieldsViewModel : ObservableObject, IConditionFields
{
    private const int NamedBiomes = 3;
    private const decimal PercentPerShare = 100;

    public string Title { get; } = "Biome";
    public string BiomesLabel { get; } = "Biomes";
    public string FilterPlaceholder { get; } = "Filter biomes";
    public string MatchLabel { get; } = "Needs";
    public string DimensionLabel { get; } = "Dimension";
    public string ScaleLabel { get; } = "Sample every";
    public string HeightLabel { get; } = "Sample at Y";
    public string ShareLabel { get; } = "Cover at least, in %";
    public string SamplesLabel { get; } = "Random samples";
    public string ShareHint { get; } = "The share is judged from random points at 4 blocks per sample, the same ones for a seed every time. More samples judge it more closely but take longer; 400 come within about 5 % of the true share.";
    public decimal MinHeight { get; } = Constants.MinSampleHeight;
    public decimal MaxHeight { get; } = Constants.MaxSampleHeight;
    public decimal MinSharePercent { get; } = 1;
    public decimal MaxSharePercent { get; } = PercentPerShare;
    public decimal MinSamples { get; } = BiomeCondition.MinSamples;
    public decimal MaxSamples { get; } = BiomeCondition.MaxSamples;

    [ObservableProperty]
    public partial ComboChoice<Dimension>? SelectedDimension { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsShare))]
    public partial ComboChoice<BiomeMatch>? SelectedMatch { get; set; }

    [ObservableProperty]
    public partial decimal? SharePercent { get; set; }

    [ObservableProperty]
    public partial decimal? Samples { get; set; }

    [ObservableProperty]
    public partial ComboChoice<int>? SelectedScale { get; set; }

    [ObservableProperty]
    public partial decimal? Height { get; set; }

    [ObservableProperty]
    public partial string Filter { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ChosenText { get; set; } = string.Empty;

    public ObservableCollection<ComboChoice<Dimension>> Dimensions { get; } =
    [
        new() { Display = "Overworld", Value = Dimension.Overworld },
        new() { Display = "Nether", Value = Dimension.Nether },
        new() { Display = "End", Value = Dimension.End },
    ];

    public ObservableCollection<ComboChoice<BiomeMatch>> Matches { get; } =
    [
        new() { Display = "One of the biomes", Value = BiomeMatch.Any },
        new() { Display = "Each of the biomes", Value = BiomeMatch.All },
        new() { Display = "A share of the area", Value = BiomeMatch.Share },
    ];

    public ObservableCollection<ComboChoice<int>> Scales { get; } =
    [
        new() { Display = "Block", Value = 1 },
        new() { Display = "4 blocks", Value = 4 },
        new() { Display = "16 blocks", Value = 16 },
        new() { Display = "64 blocks", Value = 64 },
        new() { Display = "256 blocks", Value = 256 },
    ];

    /// <summary>The biomes of the dimension that match the filter, together with the chosen ones even when the version lacks them.</summary>
    public ObservableCollection<BiomeToggleViewModel> Biomes { get; } = [];

    /// <summary>Whether the biomes have to cover a share of the area, judged from random points instead of a grid.</summary>
    public bool IsShare => SelectedMatch?.Value == BiomeMatch.Share;

    public string? Detail => _chosen.IsEmpty ? null : IsShare ? string.Create(CultureInfo.InvariantCulture, $"{DescribeChosen()} on {SharePercent ?? 0:0} % of the area") : DescribeChosen();

    private readonly IBiomeCatalog _catalog;
    private readonly Action _changed;
    private readonly List<BiomeToggleViewModel> _allBiomes = [];
    private McVersion _version;
    private BiomeSet _chosen;
    private bool _isShowing;

    public BiomeFieldsViewModel(BiomeCondition condition, McVersion version, IBiomeCatalog catalog, Action changed)
    {
        ArgumentNullException.ThrowIfNull(condition);

        _catalog = catalog;
        _changed = changed;
        _version = version;
        _chosen = condition.Biomes;
        Show(() =>
        {
            SelectedDimension = Dimensions.FirstOrDefault(choice => choice.Value == condition.Dimension) ?? Dimensions[0];
            SelectedMatch = Matches.FirstOrDefault(choice => choice.Value == condition.Match) ?? Matches[0];
            SelectedScale = Scales.FirstOrDefault(choice => choice.Value == condition.Scale) ?? Scales.First(choice => choice.Value == BiomeCondition.DefaultScale);
            Height = condition.Y;
            SharePercent = (decimal)condition.MinShare * PercentPerShare;
            Samples = condition.Samples;
            ShowBiomes();
        });
    }

    public SearchCondition Apply(SearchCondition condition)
        => condition is BiomeCondition biome
            ? biome with
            {
                Biomes = _chosen,
                Match = SelectedMatch?.Value ?? BiomeMatch.Any,
                Dimension = SelectedDimension?.Value ?? Dimension.Overworld,
                Scale = SelectedScale?.Value ?? BiomeCondition.DefaultScale,
                Y = (int)Math.Round(Height ?? ViewSettings.DefaultSampleHeight),
                MinShare = (double)((SharePercent ?? 0) / PercentPerShare),
                Samples = (int)Math.Round(Samples ?? BiomeCondition.DefaultSamples),
            }
            : condition;

    public void ShowVersion(McVersion version)
    {
        _version = version;
        Show(ShowBiomes);
    }

    /// <summary>Biomes of another dimension cannot occur in the new one, so they are no longer chosen.</summary>
    partial void OnSelectedDimensionChanged(ComboChoice<Dimension>? value)
    {
        if (_isShowing || value is null)
            return;

        _chosen = BiomeSet.Of([.. _chosen.ToArray().Where(biome => _catalog.Generates(_version, value.Value, biome))]);
        Show(ShowBiomes);
        _changed();
    }

    partial void OnSelectedMatchChanged(ComboChoice<BiomeMatch>? value) => ReportChange();

    partial void OnSelectedScaleChanged(ComboChoice<int>? value) => ReportChange();

    partial void OnHeightChanged(decimal? value) => ReportChange();

    partial void OnSharePercentChanged(decimal? value) => ReportChange();

    partial void OnSamplesChanged(decimal? value) => ReportChange();

    partial void OnFilterChanged(string value) => ShowFilteredBiomes();

    private void ShowBiomes()
    {
        var dimension = SelectedDimension?.Value ?? Dimension.Overworld;
        var biomes = _catalog.GetBiomes(_version, dimension).Union(_chosen.ToArray());

        _allBiomes.Clear();
        _allBiomes.AddRange(biomes
            .Select(biome => new BiomeToggleViewModel(biome, GetName(biome), _chosen.Contains(biome), OnBiomeToggled))
            .OrderBy(toggle => toggle.Name, StringComparer.Ordinal));
        ShowFilteredBiomes();
        ShowChosen();
    }

    private void ShowFilteredBiomes()
    {
        Biomes.Clear();
        foreach (var biome in _allBiomes.Where(biome => biome.Name.Contains(Filter.Trim(), StringComparison.OrdinalIgnoreCase)))
            Biomes.Add(biome);
    }

    private void OnBiomeToggled(BiomeToggleViewModel biome)
    {
        _chosen = biome.IsChecked ? _chosen.With(biome.Biome) : _chosen.Without(biome.Biome);
        ShowChosen();
        ReportChange();
    }

    private void ShowChosen()
        => ChosenText = _chosen.Count switch
        {
            0 => "No biome chosen yet.",
            1 => "1 biome chosen.",
            var count => string.Create(CultureInfo.InvariantCulture, $"{count} biomes chosen."),
        };

    /// <summary>Names the first chosen biomes, such as "Plains, Desert or 3 more".</summary>
    private string DescribeChosen()
    {
        string[] names = [.. _chosen.ToArray().Select(GetName).Order(StringComparer.Ordinal)];
        var conjunction = SelectedMatch?.Value == BiomeMatch.All ? " and " : " or ";
        if (names.Length <= NamedBiomes)
            return names.Length == 1 ? names[0] : string.Join(", ", names[..^1]) + conjunction + names[^1];

        var shown = names[..(NamedBiomes - 1)];
        return string.Join(", ", shown) + conjunction + string.Create(CultureInfo.InvariantCulture, $"{names.Length - shown.Length} more");
    }

    private string GetName(int biome) => _catalog.GetDisplayName(_version, biome) ?? string.Create(CultureInfo.InvariantCulture, $"Biome {biome}");

    private void ReportChange()
    {
        if (!_isShowing)
            _changed();
    }

    private void Show(Action changes)
    {
        var wasShowing = _isShowing;
        _isShowing = true;
        try
        {
            changes();
        }
        finally
        {
            _isShowing = wasShowing;
        }
    }
}

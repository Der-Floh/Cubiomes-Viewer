using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Which biomes a biome condition looks for, where and how it samples them: on a grid, or at random points for a share of the area.</summary>
public sealed partial class BiomeFieldsViewModel : ObservableObject, IConditionFields
{
    private const decimal PercentPerShare = 100;

    public string Title { get; } = "Biome";
    public string MatchLabel { get; } = "Needs";
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

    public BiomePickerViewModel Picker { get; }

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

    /// <summary>Whether the biomes have to cover a share of the area, judged from random points instead of a grid.</summary>
    public bool IsShare => SelectedMatch?.Value == BiomeMatch.Share;

    public string? Detail => Picker.Chosen.IsEmpty ? null : IsShare ? string.Create(CultureInfo.InvariantCulture, $"{Picker.Describe(" or ")} on {SharePercent ?? 0:0} % of the area") : Picker.Describe(SelectedMatch?.Value == BiomeMatch.All ? " and " : " or ");

    private readonly Action _changed;
    private bool _isShowing;

    public BiomeFieldsViewModel(BiomeCondition condition, McVersion version, IBiomeCatalog catalog, Action changed)
    {
        ArgumentNullException.ThrowIfNull(condition);

        _changed = changed;
        Picker = new BiomePickerViewModel(condition.Biomes, condition.Dimension, version, catalog, changed);
        _isShowing = true;
        SelectedMatch = Matches.FirstOrDefault(choice => choice.Value == condition.Match) ?? Matches[0];
        SelectedScale = Scales.FirstOrDefault(choice => choice.Value == condition.Scale) ?? Scales.First(choice => choice.Value == BiomeCondition.DefaultScale);
        Height = condition.Y;
        SharePercent = (decimal)condition.MinShare * PercentPerShare;
        Samples = condition.Samples;
        _isShowing = false;
    }

    public SearchCondition Apply(SearchCondition condition)
        => condition is BiomeCondition biome
            ? biome with
            {
                Biomes = Picker.Chosen,
                Match = SelectedMatch?.Value ?? BiomeMatch.Any,
                Dimension = Picker.Dimension,
                Scale = SelectedScale?.Value ?? BiomeCondition.DefaultScale,
                Y = (int)Math.Round(Height ?? ViewSettings.DefaultSampleHeight),
                MinShare = (double)((SharePercent ?? 0) / PercentPerShare),
                Samples = (int)Math.Round(Samples ?? BiomeCondition.DefaultSamples),
            }
            : condition;

    public void ShowVersion(McVersion version) => Picker.ShowVersion(version);

    partial void OnSelectedMatchChanged(ComboChoice<BiomeMatch>? value) => ReportChange();

    partial void OnSelectedScaleChanged(ComboChoice<int>? value) => ReportChange();

    partial void OnHeightChanged(decimal? value) => ReportChange();

    partial void OnSharePercentChanged(decimal? value) => ReportChange();

    partial void OnSamplesChanged(decimal? value) => ReportChange();

    private void ReportChange()
    {
        if (!_isShowing)
            _changed();
    }
}

using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Which biomes a biome-patch condition groups into patches, how large a patch is at least and how finely it samples.</summary>
public sealed partial class BiomePatchFieldsViewModel : ObservableObject, IConditionFields
{
    private const int MaxMinChunks = 1_000_000;

    public string Title { get; } = "Biome patch";
    public string MinChunksLabel { get; } = "At least, in chunks";
    public string ScaleLabel { get; } = "Sample every";
    public string HeightLabel { get; } = "Sample at Y";
    public string PatchHint { get; } = "A patch is a group of neighbouring samples with one of the biomes; each large enough patch is a match at its middle, and a patch that reaches beyond the area counts only the part inside it.";
    public decimal MinChunksMinimum { get; } = 1;
    public decimal MinChunksMaximum { get; } = MaxMinChunks;
    public decimal MinHeight { get; } = Constants.MinSampleHeight;
    public decimal MaxHeight { get; } = Constants.MaxSampleHeight;

    [ObservableProperty]
    public partial decimal? MinChunks { get; set; }

    [ObservableProperty]
    public partial ComboChoice<int>? SelectedScale { get; set; }

    [ObservableProperty]
    public partial decimal? Height { get; set; }

    public BiomePickerViewModel Picker { get; }

    public ObservableCollection<ComboChoice<int>> Scales { get; } =
    [
        new() { Display = "4 blocks", Value = 4 },
        new() { Display = "16 blocks", Value = 16 },
        new() { Display = "64 blocks", Value = 64 },
        new() { Display = "256 blocks", Value = 256 },
    ];

    public string? Detail => Picker.Chosen.IsEmpty ? null : string.Create(CultureInfo.InvariantCulture, $"{Picker.Describe(" or ")} of {MinChunks ?? 0:0} chunks or more");

    private readonly Action _changed;
    private bool _isShowing;

    public BiomePatchFieldsViewModel(BiomePatchCondition condition, McVersion version, IBiomeCatalog catalog, Action changed)
    {
        ArgumentNullException.ThrowIfNull(condition);

        _changed = changed;
        Picker = new BiomePickerViewModel(condition.Biomes, condition.Dimension, version, catalog, changed);
        _isShowing = true;
        MinChunks = condition.MinChunks;
        SelectedScale = Scales.FirstOrDefault(choice => choice.Value == condition.Scale) ?? Scales.First(choice => choice.Value == BiomePatchCondition.DefaultScale);
        Height = condition.Y;
        _isShowing = false;
    }

    public SearchCondition Apply(SearchCondition condition)
        => condition is BiomePatchCondition patch
            ? patch with
            {
                Biomes = Picker.Chosen,
                Dimension = Picker.Dimension,
                MinChunks = (int)Math.Round(MinChunks ?? BiomePatchCondition.DefaultMinChunks),
                Scale = SelectedScale?.Value ?? BiomePatchCondition.DefaultScale,
                Y = (int)Math.Round(Height ?? ViewSettings.DefaultSampleHeight),
            }
            : condition;

    public void ShowVersion(McVersion version) => Picker.ShowVersion(version);

    partial void OnMinChunksChanged(decimal? value) => ReportChange();

    partial void OnSelectedScaleChanged(ComboChoice<int>? value) => ReportChange();

    partial void OnHeightChanged(decimal? value) => ReportChange();

    private void ReportChange()
    {
        if (!_isShowing)
            _changed();
    }
}

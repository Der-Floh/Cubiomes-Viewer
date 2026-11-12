using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Which temperature category a temperature-category condition looks for.</summary>
public sealed partial class TemperatureCategoryFieldsViewModel : ObservableObject, IConditionFields
{
    public string Title { get; } = "Temperature category";
    public string CategoryLabel { get; } = "Category";
    public string CategoryHint { get; } = "From 1.7 to 1.17 the Overworld settles the climate of each region of 1,024 blocks, 4,096 with large biomes, before it picks any biome, so asking for the climate first spares the biomes of most seeds.";

    [ObservableProperty]
    public partial ComboChoice<TemperatureCategory>? SelectedCategory { get; set; }

    public ObservableCollection<ComboChoice<TemperatureCategory>> Categories { get; } =
    [
        new() { Display = "Oceanic", Value = TemperatureCategory.Oceanic },
        new() { Display = "Warm: deserts, savannas, plains", Value = TemperatureCategory.Warm },
        new() { Display = "Lush: forests, swamps, mountains, plains", Value = TemperatureCategory.Lush },
        new() { Display = "Cold: taigas, forests, mountains", Value = TemperatureCategory.Cold },
        new() { Display = "Freezing: snowy plains and taigas", Value = TemperatureCategory.Freezing },
        new() { Display = "Special warm: badlands plateaus", Value = TemperatureCategory.SpecialWarm },
        new() { Display = "Special lush: jungles", Value = TemperatureCategory.SpecialLush },
        new() { Display = "Special cold: giant tree taigas", Value = TemperatureCategory.SpecialCold },
    ];

    public string? Detail => SelectedCategory?.Display;

    private readonly Action _changed;
    private readonly bool _isShowing;

    public TemperatureCategoryFieldsViewModel(TemperatureCategoryCondition condition, Action changed)
    {
        ArgumentNullException.ThrowIfNull(condition);

        _changed = changed;
        _isShowing = true;
        SelectedCategory = Categories.FirstOrDefault(choice => choice.Value == condition.Category) ?? Categories[0];
        _isShowing = false;
    }

    public SearchCondition Apply(SearchCondition condition)
        => condition is TemperatureCategoryCondition temperature ? temperature with { Category = SelectedCategory?.Value ?? TemperatureCategory.SpecialLush } : condition;

    public void ShowVersion(McVersion version)
    {
    }

    partial void OnSelectedCategoryChanged(ComboChoice<TemperatureCategory>? value)
    {
        if (!_isShowing && value is not null)
            _changed();
    }
}

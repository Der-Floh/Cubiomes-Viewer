using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Biomes;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Picks biomes of one dimension from a list that can be filtered, for the conditions that look for biomes.</summary>
public sealed partial class BiomePickerViewModel : ObservableObject
{
    private const int NamedBiomes = 3;

    public string BiomesLabel { get; } = "Biomes";
    public string FilterPlaceholder { get; } = "Filter biomes";
    public string DimensionLabel { get; } = "Dimension";

    [ObservableProperty]
    public partial ComboChoice<Dimension>? SelectedDimension { get; set; }

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

    /// <summary>The biomes of the dimension that match the filter, together with the chosen ones even when the version lacks them.</summary>
    public ObservableCollection<BiomeToggleViewModel> Biomes { get; } = [];

    public BiomeSet Chosen { get; private set; }

    public Dimension Dimension => SelectedDimension?.Value ?? Dimension.Overworld;

    private readonly IBiomeCatalog _catalog;
    private readonly Action _changed;
    private readonly List<BiomeToggleViewModel> _allBiomes = [];
    private McVersion _version;
    private bool _isShowing;

    public BiomePickerViewModel(BiomeSet chosen, Dimension dimension, McVersion version, IBiomeCatalog catalog, Action changed)
    {
        _catalog = catalog;
        _changed = changed;
        _version = version;
        Chosen = chosen;
        Show(() =>
        {
            SelectedDimension = Dimensions.FirstOrDefault(choice => choice.Value == dimension) ?? Dimensions[0];
            ShowBiomes();
        });
    }

    public void ShowVersion(McVersion version)
    {
        _version = version;
        Show(ShowBiomes);
    }

    /// <summary>Names the first chosen biomes joined by a conjunction, such as "Plains, Desert or 3 more".</summary>
    public string Describe(string conjunction)
    {
        string[] names = [.. Chosen.ToArray().Select(GetName).Order(StringComparer.Ordinal)];
        if (names.Length <= NamedBiomes)
            return names.Length == 1 ? names[0] : string.Join(", ", names[..^1]) + conjunction + names[^1];

        var shown = names[..(NamedBiomes - 1)];
        return string.Join(", ", shown) + conjunction + string.Create(CultureInfo.InvariantCulture, $"{names.Length - shown.Length} more");
    }

    /// <summary>Biomes of another dimension cannot occur in the new one, so they are no longer chosen.</summary>
    partial void OnSelectedDimensionChanged(ComboChoice<Dimension>? value)
    {
        if (_isShowing || value is null)
            return;

        Chosen = BiomeSet.Of([.. Chosen.ToArray().Where(biome => _catalog.Generates(_version, value.Value, biome))]);
        Show(ShowBiomes);
        _changed();
    }

    partial void OnFilterChanged(string value) => ShowFilteredBiomes();

    private void ShowBiomes()
    {
        var biomes = _catalog.GetBiomes(_version, Dimension).Union(Chosen.ToArray());

        _allBiomes.Clear();
        _allBiomes.AddRange(biomes
            .Select(biome => new BiomeToggleViewModel(biome, GetName(biome), Chosen.Contains(biome), OnBiomeToggled))
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
        Chosen = biome.IsChecked ? Chosen.With(biome.Biome) : Chosen.Without(biome.Biome);
        ShowChosen();
        if (!_isShowing)
            _changed();
    }

    private void ShowChosen()
        => ChosenText = Chosen.Count switch
        {
            0 => "No biome chosen yet.",
            1 => "1 biome chosen.",
            var count => string.Create(CultureInfo.InvariantCulture, $"{count} biomes chosen."),
        };

    private string GetName(int biome) => _catalog.GetDisplayName(_version, biome) ?? string.Create(CultureInfo.InvariantCulture, $"Biome {biome}");

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

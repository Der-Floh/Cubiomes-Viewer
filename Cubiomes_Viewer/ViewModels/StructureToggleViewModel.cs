using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>
/// Switches the markers of one structure type on or off and filters them by the features of the structures: a
/// structure is marked while each of its features has a ticked option.
/// </summary>
public sealed partial class StructureToggleViewModel : ObservableObject
{
    public string FilterTip { get; } = "Filter by features";
    public string ShowAllOptionsLabel { get; } = "Show all";

    public StructureType Type { get; }
    public MarkerToggleViewModel Toggle { get; }

    /// <summary>The features the current version can tell apart; empty when the structures of the type are all alike.</summary>
    public IReadOnlyList<StructureFeatureViewModel> Features { get; }

    public bool HasFeatures => Features.Count > 0;

    /// <summary>Whether an option is unticked, so that some structures of the type are not marked.</summary>
    public bool IsFiltered => Features.Any(feature => feature.Options.Any(option => !option.IsChecked));

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    private readonly Action<StructureToggleViewModel> _filterChanged;
    private bool _isResetting;

    public StructureToggleViewModel(StructureType type, MarkerToggleViewModel toggle, IReadOnlyList<StructureFeature> features,
        Func<StructureFeature, StructureFeatureOption, bool> isShown, Action<StructureToggleViewModel> filterChanged)
    {
        ArgumentNullException.ThrowIfNull(toggle);
        ArgumentNullException.ThrowIfNull(features);
        ArgumentNullException.ThrowIfNull(isShown);
        ArgumentNullException.ThrowIfNull(filterChanged);

        Type = type;
        Toggle = toggle;
        _filterChanged = filterChanged;
        Features = [.. features.Select(feature => new StructureFeatureViewModel(feature, option => isShown(feature, option), OnOptionChanged))];
    }

    [RelayCommand]
    public void ShowAllOptions()
    {
        _isResetting = true;
        try
        {
            foreach (var option in Features.SelectMany(feature => feature.Options))
                option.IsChecked = true;
        }
        finally
        {
            _isResetting = false;
        }

        OnOptionChanged();
    }

    private void OnOptionChanged()
    {
        if (_isResetting)
            return;

        OnPropertyChanged(nameof(IsFiltered));
        _filterChanged(this);
    }
}

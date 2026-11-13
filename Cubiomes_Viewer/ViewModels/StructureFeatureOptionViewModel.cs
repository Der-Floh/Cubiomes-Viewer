using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Engine.Data.Structures;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Shows or hides the structures with one option of a feature and reports each change to its owner.</summary>
public sealed partial class StructureFeatureOptionViewModel : ObservableObject
{
    public string Key { get; }
    public string Name { get; }

    [ObservableProperty]
    public partial bool IsChecked { get; set; }

    // Unset while the constructor assigns the initial state, which is not a change to report.
    private readonly Action? _changed;

    public StructureFeatureOptionViewModel(StructureFeatureOption option, bool isChecked, Action changed)
    {
        ArgumentNullException.ThrowIfNull(option);

        Key = option.Key;
        Name = option.Name;
        IsChecked = isChecked;
        _changed = changed;
    }

    partial void OnIsCheckedChanged(bool value) => _changed?.Invoke();
}

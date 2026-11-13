using Cubiomes_Viewer.Engine.Data.Structures;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>A feature of a structure type whose ticked options are the alternatives a marked structure may have.</summary>
public sealed class StructureFeatureViewModel
{
    public string Key { get; }
    public string Name { get; }
    public IReadOnlyList<StructureFeatureOptionViewModel> Options { get; }

    public StructureFeatureViewModel(StructureFeature feature, Func<StructureFeatureOption, bool> isShown, Action changed)
    {
        ArgumentNullException.ThrowIfNull(feature);
        ArgumentNullException.ThrowIfNull(isShown);

        Key = feature.Key;
        Name = feature.Name;
        Options = [.. feature.Options.Select(option => new StructureFeatureOptionViewModel(option, isShown(option), changed))];
    }
}

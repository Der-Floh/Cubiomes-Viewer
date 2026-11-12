using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Which structure a structure condition looks for, and what the structure has to have.</summary>
public sealed partial class StructureFieldsViewModel : ObservableObject, IConditionFields
{
    private static readonly Dimension[] _dimensions = [Dimension.Overworld, Dimension.Nether, Dimension.End];

    private static readonly ComboChoice<StructureTrait>[] _traits =
    [
        new() { Display = "Any", Value = StructureTrait.None },
        new() { Display = "With an end ship", Value = StructureTrait.EndShip },
        new() { Display = "Abandoned", Value = StructureTrait.Abandoned },
        new() { Display = "With a basement", Value = StructureTrait.Basement },
        new() { Display = "Giant", Value = StructureTrait.Giant },
    ];

    public string TypeLabel { get; } = "Structure";
    public string TraitLabel { get; } = "Kind";

    [ObservableProperty]
    public partial ComboChoice<StructureType>? SelectedType { get; set; }

    [ObservableProperty]
    public partial ComboChoice<StructureTrait>? SelectedTrait { get; set; }

    [ObservableProperty]
    public partial bool HasTraits { get; set; }

    /// <summary>The structure types of the version, and the chosen one even when the version lacks it.</summary>
    public ObservableCollection<ComboChoice<StructureType>> Types { get; } = [];

    /// <summary>The traits structures of the chosen type can have, after "any".</summary>
    public ObservableCollection<ComboChoice<StructureTrait>> Traits { get; } = [];

    public string Title => SelectedType?.Display ?? TypeLabel;

    public string? Detail => _trait == StructureTrait.None ? null : SelectedTrait?.Display.ToLowerInvariant();

    private readonly IStructureCatalog _catalog;
    private readonly Action _changed;
    private StructureType _type;
    private StructureTrait _trait;
    private bool _isShowing;

    public StructureFieldsViewModel(StructureCondition condition, McVersion version, IStructureCatalog catalog, Action changed)
    {
        ArgumentNullException.ThrowIfNull(condition);

        _catalog = catalog;
        _changed = changed;
        _type = condition.Type;
        _trait = condition.Trait;
        ShowVersion(version);
    }

    public SearchCondition Apply(SearchCondition condition)
        => condition is StructureCondition structure ? structure with { Type = _type, Trait = _trait } : condition;

    public void ShowVersion(McVersion version)
    {
        var available = _dimensions.SelectMany(dimension => _catalog.GetAvailable(version, dimension)).Select(info => info.Type).ToHashSet();
        Show(() =>
        {
            Types.Clear();
            foreach (var info in _catalog.Structures.Where(info => available.Contains(info.Type) || info.Type == _type))
                Types.Add(new ComboChoice<StructureType> { Display = info.Name, Value = info.Type });
            SelectedType = Types.FirstOrDefault(choice => choice.Value == _type);
            ShowTraits();
        });
    }

    /// <summary>A trait the new type cannot have gives way to any structure of the type.</summary>
    partial void OnSelectedTypeChanged(ComboChoice<StructureType>? value)
    {
        if (_isShowing || value is null)
            return;

        _type = value.Value;
        if (!_trait.AppliesTo(_type))
            _trait = StructureTrait.None;
        Show(ShowTraits);
        _changed();
    }

    partial void OnSelectedTraitChanged(ComboChoice<StructureTrait>? value)
    {
        if (_isShowing || value is null)
            return;

        _trait = value.Value;
        _changed();
    }

    private void ShowTraits()
    {
        Traits.Clear();
        foreach (var trait in _traits.Where(choice => choice.Value.AppliesTo(_type)))
            Traits.Add(trait);
        SelectedTrait = Traits.FirstOrDefault(choice => choice.Value == _trait) ?? Traits[0];
        HasTraits = Traits.Count > 1;
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

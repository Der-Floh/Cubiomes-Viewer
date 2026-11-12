using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Which structure a structure condition looks for.</summary>
public sealed partial class StructureFieldsViewModel : ObservableObject, IConditionFields
{
    private static readonly Dimension[] _dimensions = [Dimension.Overworld, Dimension.Nether, Dimension.End];

    public string TypeLabel { get; } = "Structure";

    [ObservableProperty]
    public partial ComboChoice<StructureType>? SelectedType { get; set; }

    /// <summary>The structure types of the version, and the chosen one even when the version lacks it.</summary>
    public ObservableCollection<ComboChoice<StructureType>> Types { get; } = [];

    public string Title => SelectedType?.Display ?? TypeLabel;
    public string? Detail => null;

    private readonly IStructureCatalog _catalog;
    private readonly Action _changed;
    private StructureType _type;
    private bool _isShowing;

    public StructureFieldsViewModel(StructureCondition condition, McVersion version, IStructureCatalog catalog, Action changed)
    {
        ArgumentNullException.ThrowIfNull(condition);

        _catalog = catalog;
        _changed = changed;
        _type = condition.Type;
        ShowVersion(version);
    }

    public SearchCondition Apply(SearchCondition condition)
        => condition is StructureCondition structure ? structure with { Type = _type } : condition;

    public void ShowVersion(McVersion version)
    {
        var available = _dimensions.SelectMany(dimension => _catalog.GetAvailable(version, dimension)).Select(info => info.Type).ToHashSet();
        _isShowing = true;
        try
        {
            Types.Clear();
            foreach (var info in _catalog.Structures.Where(info => available.Contains(info.Type) || info.Type == _type))
                Types.Add(new ComboChoice<StructureType> { Display = info.Name, Value = info.Type });
            SelectedType = Types.FirstOrDefault(choice => choice.Value == _type);
        }
        finally
        {
            _isShowing = false;
        }
    }

    partial void OnSelectedTypeChanged(ComboChoice<StructureType>? value)
    {
        if (_isShowing || value is null)
            return;

        _type = value.Value;
        _changed();
    }
}

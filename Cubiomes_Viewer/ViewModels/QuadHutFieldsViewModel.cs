using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Which huts a quad-huts condition looks for and how close together they have to be.</summary>
public sealed partial class QuadHutFieldsViewModel : ObservableObject, IConditionFields
{
    private static readonly StructureType[] _types = [StructureType.SwampHut, StructureType.DesertPyramid, StructureType.JungleTemple, StructureType.Igloo];

    public string TypeLabel { get; } = "Structure";
    public string QualityLabel { get; } = "The huts at least as close as";

    [ObservableProperty]
    public partial ComboChoice<StructureType>? SelectedType { get; set; }

    [ObservableProperty]
    public partial ComboChoice<QuadQuality>? SelectedQuality { get; set; }

    public ObservableCollection<ComboChoice<StructureType>> Types { get; }

    public ObservableCollection<ComboChoice<QuadQuality>> Qualities { get; } =
    [
        new() { Display = "An ideal constellation", Value = QuadQuality.Ideal },
        new() { Display = "A classic constellation", Value = QuadQuality.Classic },
        new() { Display = "Room for a 43-block drop", Value = QuadQuality.Normal },
        new() { Display = "All four in range of one point", Value = QuadQuality.Barely },
    ];

    public string Title => $"Quad {(SelectedType?.Display ?? TypeLabel).ToLowerInvariant()}s";

    public string? Detail => SelectedQuality?.Value switch
    {
        QuadQuality.Ideal => "ideal",
        QuadQuality.Classic => "classic or better",
        QuadQuality.Normal => "room for a drop",
        _ => null,
    };

    // Unset while the constructor assigns the initial state, which is not a change to report.
    private readonly Action? _changed;

    public QuadHutFieldsViewModel(QuadHutCondition condition, IStructureCatalog catalog, Action changed)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(catalog);

        Types = [.. _types.Select(type => new ComboChoice<StructureType> { Display = catalog.GetInfo(type).Name, Value = type })];
        SelectedType = Types.FirstOrDefault(choice => choice.Value == condition.Type) ?? Types[0];
        SelectedQuality = Qualities.FirstOrDefault(choice => choice.Value == condition.Quality) ?? Qualities[^1];
        _changed = changed;
    }

    public SearchCondition Apply(SearchCondition condition)
        => condition is QuadHutCondition quad
            ? quad with { Type = SelectedType?.Value ?? StructureType.SwampHut, Quality = SelectedQuality?.Value ?? QuadQuality.Barely }
            : condition;

    /// <summary>Every version offers the same huts; the validation says which of them a version lacks.</summary>
    public void ShowVersion(McVersion version)
    {
    }

    partial void OnSelectedTypeChanged(ComboChoice<StructureType>? value) => _changed?.Invoke();

    partial void OnSelectedQualityChanged(ComboChoice<QuadQuality>? value) => _changed?.Invoke();
}

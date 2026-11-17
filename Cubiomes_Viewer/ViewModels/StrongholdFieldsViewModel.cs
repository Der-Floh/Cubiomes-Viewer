using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.LootServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>How many eyes of ender the end portal of a stronghold a stronghold condition looks for already holds, and what its chests hold.</summary>
public sealed partial class StrongholdFieldsViewModel : ObservableObject, IConditionFields, ILootConditionFields
{
    public string Title { get; } = "Stronghold";
    public string PortalEyesLabel { get; } = "Eyes already in its end portal, at least";
    public string PortalEyesHint { get; } = "Each of the 12 frames holds an eye by a chance of one in ten, known from 1.13 on. At 0 any stronghold counts; at 12 the portal is open.";
    public decimal PortalEyesMinimum { get; } = 0;
    public decimal PortalEyesMaximum { get; } = StrongholdCondition.PortalFrames;

    [ObservableProperty]
    public partial decimal? PortalEyes { get; set; }

    public LootFieldsViewModel Loot { get; }

    public string? Detail
    {
        get
        {
            string?[] parts = [DescribeEyes(), Loot.Detail];
            return parts.Any(part => part is not null) ? string.Join(", ", parts.Where(part => part is not null)) : null;
        }
    }

    private int Eyes => (int)Math.Clamp(Math.Round(PortalEyes ?? 0), PortalEyesMinimum, PortalEyesMaximum);

    private readonly Action _changed;
    private readonly bool _isShowing;

    public StrongholdFieldsViewModel(StrongholdCondition condition, McVersion version, ILootCatalog lootCatalog, Action changed)
    {
        ArgumentNullException.ThrowIfNull(condition);

        _changed = changed;
        _isShowing = true;
        PortalEyes = condition.PortalEyes;
        _isShowing = false;
        Loot = new LootFieldsViewModel(condition.Loot, StructureType.Stronghold, version, lootCatalog, changed);
    }

    public SearchCondition Apply(SearchCondition condition)
        => condition is StrongholdCondition stronghold ? stronghold with { PortalEyes = Eyes, Loot = Loot.Requirement } : condition;

    public void ShowVersion(McVersion version) => Loot.ShowVersion(version);

    private string? DescribeEyes() => Eyes switch
    {
        0 => null,
        StrongholdCondition.PortalFrames => "with its end portal open",
        _ => string.Create(CultureInfo.InvariantCulture, $"with {Eyes}+ eyes in its portal"),
    };

    partial void OnPortalEyesChanged(decimal? value)
    {
        if (!_isShowing)
            _changed();
    }
}

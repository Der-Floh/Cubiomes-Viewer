using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>How many eyes of ender the end portal of a stronghold a stronghold condition looks for already holds.</summary>
public sealed partial class StrongholdFieldsViewModel : ObservableObject, IConditionFields
{
    public string Title { get; } = "Stronghold";
    public string PortalEyesLabel { get; } = "Eyes already in its end portal, at least";
    public string PortalEyesHint { get; } = "Each of the 12 frames holds an eye by a chance of one in ten, known from 1.13 on. At 0 any stronghold counts; at 12 the portal is open.";
    public decimal PortalEyesMinimum { get; } = 0;
    public decimal PortalEyesMaximum { get; } = StrongholdCondition.PortalFrames;

    [ObservableProperty]
    public partial decimal? PortalEyes { get; set; }

    public string? Detail => Eyes switch
    {
        0 => null,
        StrongholdCondition.PortalFrames => "with its end portal open",
        _ => string.Create(CultureInfo.InvariantCulture, $"with {Eyes}+ eyes in its portal"),
    };

    private int Eyes => (int)Math.Clamp(Math.Round(PortalEyes ?? 0), PortalEyesMinimum, PortalEyesMaximum);

    private readonly Action _changed;
    private readonly bool _isShowing;

    public StrongholdFieldsViewModel(StrongholdCondition condition, Action changed)
    {
        ArgumentNullException.ThrowIfNull(condition);

        _changed = changed;
        _isShowing = true;
        PortalEyes = condition.PortalEyes;
        _isShowing = false;
    }

    public SearchCondition Apply(SearchCondition condition)
        => condition is StrongholdCondition stronghold ? stronghold with { PortalEyes = Eyes } : condition;

    public void ShowVersion(McVersion version)
    {
    }

    partial void OnPortalEyesChanged(decimal? value)
    {
        if (!_isShowing)
            _changed();
    }
}

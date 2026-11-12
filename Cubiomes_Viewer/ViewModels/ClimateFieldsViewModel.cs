using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.LayerServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Which climate parameter a climate condition samples, the range its values have to lie in, and where.</summary>
public sealed partial class ClimateFieldsViewModel : ObservableObject, IConditionFields
{
    private static readonly ClimateParameter[] _parameters = Enum.GetValues<ClimateParameter>();

    private static readonly Dictionary<ClimateParameter, string> _hints = new()
    {
        [ClimateParameter.Temperature] = "From cold to hot: snowy biomes lie low, deserts and badlands high.",
        [ClimateParameter.Humidity] = "From dry to wet: deserts and savannas lie low, jungles high.",
        [ClimateParameter.Continentalness] = "Oceans lie below -0.19 and deep oceans below -0.455, coasts reach up to -0.11, and above that the land lies further inland the higher the value.",
        [ClimateParameter.Erosion] = "Low erosion raises mountains and peaks; high erosion flattens the land and makes room for swamps.",
        [ClimateParameter.Depth] = "About 0 at the surface and higher further down; cave biomes such as the deep dark need a high depth.",
        [ClimateParameter.Weirdness] = "Picks between biome variants and shapes valleys and peaks; rivers run where it is close to 0.",
    };

    public string ParameterLabel { get; } = "Parameter";
    public string MinLabel { get; } = "From";
    public string MaxLabel { get; } = "To";
    public string MatchLabel { get; } = "In range";
    public string ScaleLabel { get; } = "Sample every";
    public string HeightLabel { get; } = "Sample at Y";
    public decimal MinValue { get; } = -(decimal)ClimateCondition.Limit;
    public decimal MaxValue { get; } = (decimal)ClimateCondition.Limit;
    public decimal MinHeight { get; } = Constants.MinSampleHeight;
    public decimal MaxHeight { get; } = Constants.MaxSampleHeight;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHeight), nameof(ParameterHint))]
    public partial ComboChoice<ClimateParameter>? SelectedParameter { get; set; }

    [ObservableProperty]
    public partial ComboChoice<ClimateMatch>? SelectedMatch { get; set; }

    [ObservableProperty]
    public partial decimal? Min { get; set; }

    [ObservableProperty]
    public partial decimal? Max { get; set; }

    [ObservableProperty]
    public partial ComboChoice<int>? SelectedScale { get; set; }

    [ObservableProperty]
    public partial decimal? Height { get; set; }

    /// <summary>The parameters of the version, and the chosen one even when the version lacks it.</summary>
    public ObservableCollection<ComboChoice<ClimateParameter>> Parameters { get; } = [];

    public ObservableCollection<ComboChoice<ClimateMatch>> Matches { get; } =
    [
        new() { Display = "Everywhere in the area", Value = ClimateMatch.Everywhere },
        new() { Display = "Somewhere in the area", Value = ClimateMatch.Somewhere },
        new() { Display = "On average", Value = ClimateMatch.OnAverage },
    ];

    public ObservableCollection<ComboChoice<int>> Scales { get; } =
    [
        new() { Display = "4 blocks", Value = 4 },
        new() { Display = "16 blocks", Value = 16 },
        new() { Display = "64 blocks", Value = 64 },
        new() { Display = "256 blocks", Value = 256 },
    ];

    /// <summary>Only the depth changes with the height it is sampled at.</summary>
    public bool HasHeight => SelectedParameter?.Value == ClimateParameter.Depth;

    public string ParameterHint => SelectedParameter is { } parameter && _hints.TryGetValue(parameter.Value, out var hint) ? hint : string.Empty;

    public string Title => SelectedParameter?.Display ?? "Climate";

    public string? Detail => string.Create(CultureInfo.InvariantCulture, $"{Min ?? 0:0.00} to {Max ?? 0:0.00} {DescribeMatch()}");

    private readonly ILayerCatalog _catalog;
    private readonly Action _changed;
    private ClimateParameter _parameter;
    private bool _isShowing;

    public ClimateFieldsViewModel(ClimateCondition condition, McVersion version, ILayerCatalog catalog, Action changed)
    {
        ArgumentNullException.ThrowIfNull(condition);

        _catalog = catalog;
        _changed = changed;
        _parameter = condition.Parameter;
        Show(() =>
        {
            SelectedMatch = Matches.FirstOrDefault(choice => choice.Value == condition.Match) ?? Matches[0];
            SelectedScale = Scales.FirstOrDefault(choice => choice.Value == condition.Scale) ?? Scales.First(choice => choice.Value == ClimateCondition.DefaultScale);
            Min = (decimal)condition.Min;
            Max = (decimal)condition.Max;
            Height = condition.Y;
        });
        ShowVersion(version);
    }

    public SearchCondition Apply(SearchCondition condition)
        => condition is ClimateCondition climate
            ? climate with
            {
                Parameter = _parameter,
                Match = SelectedMatch?.Value ?? ClimateMatch.Everywhere,
                Min = (double)(Min ?? 0),
                Max = (double)(Max ?? 0),
                Scale = SelectedScale?.Value ?? ClimateCondition.DefaultScale,
                Y = (int)Math.Round(Height ?? ViewSettings.DefaultSampleHeight),
            }
            : condition;

    public void ShowVersion(McVersion version)
        => Show(() =>
        {
            Parameters.Clear();
            foreach (var parameter in _parameters.Where(parameter => parameter == _parameter || _catalog.IsAvailable(parameter.ToLayerMode(), version, Dimension.Overworld)))
                Parameters.Add(new ComboChoice<ClimateParameter> { Display = _catalog.GetInfo(parameter.ToLayerMode(), version, Dimension.Overworld).Name, Value = parameter });
            SelectedParameter = Parameters.FirstOrDefault(choice => choice.Value == _parameter);
        });

    partial void OnSelectedParameterChanged(ComboChoice<ClimateParameter>? value)
    {
        if (_isShowing || value is null)
            return;

        _parameter = value.Value;
        _changed();
    }

    partial void OnSelectedMatchChanged(ComboChoice<ClimateMatch>? value) => ReportChange();

    partial void OnMinChanged(decimal? value) => ReportChange();

    partial void OnMaxChanged(decimal? value) => ReportChange();

    partial void OnSelectedScaleChanged(ComboChoice<int>? value) => ReportChange();

    partial void OnHeightChanged(decimal? value) => ReportChange();

    private string DescribeMatch() => SelectedMatch?.Value switch
    {
        ClimateMatch.Somewhere => "somewhere",
        ClimateMatch.OnAverage => "on average",
        _ => "everywhere",
    };

    private void ReportChange()
    {
        if (!_isShowing)
            _changed();
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

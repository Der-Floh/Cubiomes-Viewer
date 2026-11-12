using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>How far out one structure type stays marked on the map.</summary>
public sealed partial class StructureZoomLimitViewModel : ObservableObject
{
    private static readonly double[] _limits = [0.5, 1, 2, 4, 8, 16, 32, 64, 128, 256];

    public StructureType Type { get; }
    public string Name { get; }
    public double DefaultLimit { get; }
    public IReadOnlyList<ComboChoice<double>> Choices { get; }

    [ObservableProperty]
    public partial ComboChoice<double>? SelectedLimit { get; set; }

    public bool IsChanged => SelectedLimit is { } limit && limit.Value != DefaultLimit;

    // Unset while the constructor assigns the stored limit, which is not a change to report.
    private readonly Action<StructureZoomLimitViewModel>? _changed;

    public StructureZoomLimitViewModel(StructureType type, string name, double defaultLimit, double limit, Action<StructureZoomLimitViewModel> changed)
    {
        Type = type;
        Name = name;
        DefaultLimit = defaultLimit;
        Choices = [.. _limits.Union([defaultLimit]).Order().Select(value => new ComboChoice<double> { Display = Describe(value, value == defaultLimit), Value = value })];
        SelectedLimit = Choices.FirstOrDefault(choice => choice.Value == limit) ?? Choices.First(choice => choice.Value == defaultLimit);
        _changed = changed;
    }

    partial void OnSelectedLimitChanged(ComboChoice<double>? value) => _changed?.Invoke(this);

    private static string Describe(double blocksPerPixel, bool isDefault)
    {
        var text = string.Create(CultureInfo.InvariantCulture, $"1 px = {blocksPerPixel:0.##} blocks");
        return isDefault ? $"{text} (default)" : text;
    }
}

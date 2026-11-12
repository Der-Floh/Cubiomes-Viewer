using Avalonia.Media;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Engine.Data.Colors;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>One biome in the colour editor: its colour in the map being edited and its default one.</summary>
public sealed partial class BiomeColorEntryViewModel : ObservableObject
{
    public int Biome { get; }
    public string Name { get; }
    public RgbColor DefaultColor { get; }
    public string DefaultLabel { get; } = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsChanged), nameof(Hex))]
    public partial Color Color { get; set; }

    public RgbColor RgbColor => new(Color.R, Color.G, Color.B);
    public bool IsChanged => RgbColor != DefaultColor;
    public string Hex => RgbColor.ToHex();

    // Unset while the constructor assigns the first colour, which is not a change to report.
    private readonly Action? _changed;

    public BiomeColorEntryViewModel(int biome, string name, RgbColor defaultColor, RgbColor color, Action changed)
    {
        Biome = biome;
        Name = name;
        DefaultColor = defaultColor;
        Color = ToColor(color);
        _changed = changed;
    }

    public void SetColor(RgbColor color) => Color = ToColor(color);

    [RelayCommand]
    public void UseDefault() => SetColor(DefaultColor);

    partial void OnColorChanged(Color value) => _changed?.Invoke();

    private static Color ToColor(RgbColor color) => Color.FromRgb(color.Red, color.Green, color.Blue);
}

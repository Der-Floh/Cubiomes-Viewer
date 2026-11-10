using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Data;

/// <summary>The world that is currently open; shared by the view models that edit or display it.</summary>
public sealed partial class WorldSession : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Key))]
    public partial long Seed { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Key))]
    public partial McVersion Version { get; set; } = McVersion.V1_21_3;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Key))]
    public partial bool LargeBiomes { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Key))]
    public partial Dimension Dimension { get; set; } = Dimension.Overworld;

    [ObservableProperty]
    public partial int SampleHeight { get; set; } = ViewSettings.DefaultSampleHeight;

    public WorldKey Key => new(Seed, Version, Dimension, GeneratorFlags.ForceOceanVariants | (LargeBiomes ? GeneratorFlags.LargeBiomes : GeneratorFlags.None));

    public override string ToString() => $"{Seed} ({Version}, {Dimension})";
}

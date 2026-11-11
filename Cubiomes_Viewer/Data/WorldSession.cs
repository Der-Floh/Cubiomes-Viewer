using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Engine.Data.Maps;
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

    /// <summary>What the map shows of the world.</summary>
    [ObservableProperty]
    public partial LayerMode Layer { get; set; }

    /// <summary>How many of its widest noise octaves a climate layer shows; zero shows all of them.</summary>
    [ObservableProperty]
    public partial int Octaves { get; set; }

    public WorldKey Key => new(Seed, Version, Dimension, GeneratorFlags.ForceOceanVariants | (LargeBiomes ? GeneratorFlags.LargeBiomes : GeneratorFlags.None));

    public override string ToString() => $"{Seed} ({Version}, {Dimension})";
}

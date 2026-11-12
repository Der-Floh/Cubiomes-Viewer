using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>An entry of the recent seeds, with the command that opens its seed again.</summary>
public sealed partial class RecentSeedViewModel : ObservableObject
{
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    public partial long Seed { get; set; }

    public string Display => Seed.ToString(CultureInfo.InvariantCulture);
    public IRelayCommand OpenCommand { get; }

    public RecentSeedViewModel(long seed, Action<long> open)
    {
        ArgumentNullException.ThrowIfNull(open);

        Seed = seed;
        OpenCommand = new RelayCommand(() => open(Seed));
    }
}

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Service.NotificationServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Looks at the seed on the map or the found seeds more closely: their biomes, structures and locations.</summary>
public sealed partial class AnalysisPanelViewModel : ViewModelBase
{
    public string Title { get; } = "Analysis";
    public string ScopeLabel { get; } = "What to look at";

    public AnalysisScopeViewModel Scope { get; }

    public IReadOnlyList<AnalysisTabViewModel> Tabs { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SelectedTab))]
    public partial int SelectedTabIndex { get; set; }

    /// <summary>The open tab, whose run button and status the foot of the panel shows.</summary>
    public AnalysisTabViewModel SelectedTab => Tabs[Math.Clamp(SelectedTabIndex, 0, Tabs.Count - 1)];

    public AnalysisPanelViewModel(AnalysisScopeViewModel scope, BiomeAnalysisViewModel biomes, StructureAnalysisViewModel structures, LocationAnalysisViewModel locations, INotificationService notifications) : base(notifications)
    {
        Scope = scope;
        Tabs = [biomes, structures, locations];
    }
}

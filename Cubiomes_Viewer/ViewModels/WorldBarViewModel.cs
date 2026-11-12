using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Seeds;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.NotificationServ;

namespace Cubiomes_Viewer.ViewModels;

public sealed partial class WorldBarViewModel : ViewModelBase
{
    private const McVersion FirstLargeBiomesVersion = McVersion.V1_3_2;

    public string SeedPlaceholder { get; } = "Seed (empty for random)";
    public string RandomSeedLabel { get; } = "Random seed";
    public string RecentSeedsLabel { get; } = "Recent";
    public string LargeBiomesLabel { get; } = "Large biomes";
    public string SampleHeightLabel { get; } = "Y";
    public string GoToLabel { get; } = "Go to coordinates";
    public string GoToXLabel { get; } = "X";
    public string GoToZLabel { get; } = "Z";
    public string GoLabel { get; } = "Go";
    public string OriginLabel { get; } = "Go to origin";
    public string ZoomInLabel { get; } = "Zoom in";
    public string ZoomOutLabel { get; } = "Zoom out";
    public decimal MinSampleHeight { get; } = Constants.MinSampleHeight;
    public decimal MaxSampleHeight { get; } = Constants.MaxSampleHeight;

    [ObservableProperty]
    public partial string SeedText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SeedHint { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ComboChoice<McVersion>? SelectedVersion { get; set; }

    [ObservableProperty]
    public partial bool LargeBiomes { get; set; }

    [ObservableProperty]
    public partial ComboChoice<Dimension>? SelectedDimension { get; set; }

    [ObservableProperty]
    public partial decimal? SampleHeight { get; set; }

    [ObservableProperty]
    public partial bool HasRecentSeeds { get; set; }

    [ObservableProperty]
    public partial decimal? GoToX { get; set; } = 0;

    [ObservableProperty]
    public partial decimal? GoToZ { get; set; } = 0;

    /// <summary>Whether the versions the generator supports less completely are offered too; the open world's version always is.</summary>
    [ObservableProperty]
    public partial bool IncludeExperimentalVersions { get; set; }

    public bool IsLargeBiomesAvailable => _session.Version >= FirstLargeBiomesVersion;

    /// <summary>The versions offered, newest first.</summary>
    public ObservableCollection<ComboChoice<McVersion>> Versions { get; } = [];

    public ObservableCollection<ComboChoice<Dimension>> Dimensions { get; } =
    [
        new() { Display = "Overworld", Value = Dimension.Overworld },
        new() { Display = "Nether", Value = Dimension.Nether },
        new() { Display = "End", Value = Dimension.End },
    ];

    /// <summary>The recently opened seeds, newest first.</summary>
    public ObservableCollection<RecentSeedViewModel> RecentSeeds { get; } = [];

    public MapViewModel Map { get; }

    private readonly WorldSession _session;
    private readonly ISeedParser _seedParser;
    private readonly ISeedHistoryService _seedHistory;
    private readonly IVersionCatalog _versionCatalog;

    public WorldBarViewModel(WorldSession session, MapViewModel map, ISeedParser seedParser, ISeedHistoryService seedHistory, IVersionCatalog versionCatalog, INotificationService notificationService) : base(notificationService)
    {
        _session = session;
        _seedParser = seedParser;
        _seedHistory = seedHistory;
        _versionCatalog = versionCatalog;
        Map = map;

        ShowVersions();
        ShowSession();
        ShowRecentSeeds();
        session.PropertyChanged += OnSessionChanged;
        seedHistory.Changed += ShowRecentSeeds;
    }

    partial void OnSelectedVersionChanged(ComboChoice<McVersion>? value)
    {
        if (value is not null)
            _session.Version = value.Value;
    }

    partial void OnLargeBiomesChanged(bool value) => _session.LargeBiomes = value;

    partial void OnIncludeExperimentalVersionsChanged(bool value)
    {
        ShowVersions();
        SelectedVersion = Versions.FirstOrDefault(choice => choice.Value == _session.Version);
    }

    partial void OnSelectedDimensionChanged(ComboChoice<Dimension>? value)
    {
        if (value is not null)
            _session.Dimension = value.Value;
    }

    partial void OnSampleHeightChanged(decimal? value)
    {
        if (value is { } height)
            _session.SampleHeight = (int)height;
    }

    [RelayCommand]
    public void ApplySeed() => OpenSeed(_seedParser.Parse(SeedText));

    [RelayCommand]
    public void RandomSeed() => OpenSeed(_seedParser.Parse(null));

    [RelayCommand]
    public void GoTo() => Map.GoTo((double)(GoToX ?? 0), (double)(GoToZ ?? 0));

    private void OpenRecentSeed(long seed) => OpenSeed(new SeedParseResult(seed, SeedKind.Numeric));

    private void OpenSeed(SeedParseResult result)
    {
        _session.Seed = result.Seed;
        _seedHistory.Add(result.Seed);

        SeedText = FormatSeed(result.Seed);
        SeedHint = result.Kind switch
        {
            SeedKind.Numeric => string.Empty,
            SeedKind.Text => "from text",
            SeedKind.Random => "random",
            _ => throw new NotSupportedException($"Unsupported seed kind: {result.Kind}"),
        };
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e) => ShowSession();

    /// <summary>
    /// Offers the versions the setting asks for and the open world's. A version is only taken out when the setting
    /// changes, so that leaving an experimental version does not change the list while it is being picked from.
    /// </summary>
    private void ShowVersions()
    {
        var offered = _versionCatalog.Versions.Reverse()
            .Where(version => IncludeExperimentalVersions || !_versionCatalog.IsExperimental(version) || version == _session.Version)
            .ToList();
        if (Versions.Select(choice => choice.Value).SequenceEqual(offered))
            return;

        Versions.Clear();
        foreach (var version in offered)
            Versions.Add(new ComboChoice<McVersion> { Display = _versionCatalog.GetName(version), Value = version });
    }

    private void ShowSession()
    {
        if (Versions.All(choice => choice.Value != _session.Version))
            ShowVersions();

        if (!long.TryParse(SeedText, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var shown) || shown != _session.Seed)
        {
            SeedText = FormatSeed(_session.Seed);
            SeedHint = string.Empty;
        }

        SelectedVersion = Versions.FirstOrDefault(choice => choice.Value == _session.Version);
        SelectedDimension = Dimensions.FirstOrDefault(choice => choice.Value == _session.Dimension);
        LargeBiomes = _session.LargeBiomes;
        SampleHeight = _session.SampleHeight;
        OnPropertyChanged(nameof(IsLargeBiomesAvailable));
    }

    /// <summary>
    /// Shows the history in the entries there are and only adds or removes entries at the end, so that an entry
    /// being clicked stays in its menu, which then closes as usual.
    /// </summary>
    private void ShowRecentSeeds()
    {
        var seeds = _seedHistory.Seeds;
        while (RecentSeeds.Count > seeds.Count)
            RecentSeeds.RemoveAt(RecentSeeds.Count - 1);

        for (var index = 0; index < seeds.Count; index++)
        {
            if (index < RecentSeeds.Count)
                RecentSeeds[index].Seed = seeds[index];
            else
                RecentSeeds.Add(new RecentSeedViewModel(seeds[index], OpenRecentSeed));
        }

        HasRecentSeeds = RecentSeeds.Count > 0;
    }

    private static string FormatSeed(long seed) => seed.ToString(CultureInfo.InvariantCulture);
}

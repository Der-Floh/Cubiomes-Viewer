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
    public string RecentSeedsPlaceholder { get; } = "Recent";
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
    public partial ComboChoice<long>? SelectedRecentSeed { get; set; }

    [ObservableProperty]
    public partial decimal? GoToX { get; set; } = 0;

    [ObservableProperty]
    public partial decimal? GoToZ { get; set; } = 0;

    public bool IsLargeBiomesAvailable => _session.Version >= FirstLargeBiomesVersion;

    public ObservableCollection<ComboChoice<McVersion>> Versions { get; }

    public ObservableCollection<ComboChoice<Dimension>> Dimensions { get; } =
    [
        new() { Display = "Overworld", Value = Dimension.Overworld },
        new() { Display = "Nether", Value = Dimension.Nether },
        new() { Display = "End", Value = Dimension.End },
    ];

    public ObservableCollection<ComboChoice<long>> RecentSeeds { get; } = [];

    public MapViewModel Map { get; }

    private readonly WorldSession _session;
    private readonly ISeedParser _seedParser;
    private readonly ISeedHistoryService _seedHistory;

    public WorldBarViewModel(WorldSession session, MapViewModel map, ISeedParser seedParser, ISeedHistoryService seedHistory, IVersionCatalog versionCatalog, INotificationService notificationService) : base(notificationService)
    {
        _session = session;
        _seedParser = seedParser;
        _seedHistory = seedHistory;
        Map = map;

        Versions = [.. versionCatalog.Versions.Reverse().Select(version => new ComboChoice<McVersion> { Display = versionCatalog.GetName(version), Value = version })];

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

    partial void OnSelectedRecentSeedChanged(ComboChoice<long>? value)
    {
        if (value is null)
            return;

        OpenSeed(new SeedParseResult(value.Value, SeedKind.Numeric));
        SelectedRecentSeed = null;
    }

    [RelayCommand]
    public void ApplySeed() => OpenSeed(_seedParser.Parse(SeedText));

    [RelayCommand]
    public void RandomSeed() => OpenSeed(_seedParser.Parse(null));

    [RelayCommand]
    public void GoTo() => Map.GoTo((double)(GoToX ?? 0), (double)(GoToZ ?? 0));

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

    private void ShowSession()
    {
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

    private void ShowRecentSeeds()
    {
        RecentSeeds.Clear();
        foreach (var seed in _seedHistory.Seeds)
            RecentSeeds.Add(new ComboChoice<long> { Display = FormatSeed(seed), Value = seed });
    }

    private static string FormatSeed(long seed) => seed.ToString(CultureInfo.InvariantCulture);
}

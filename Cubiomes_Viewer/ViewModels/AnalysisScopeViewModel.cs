using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SearchServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Which seeds the analyses look at, and the area around which point.</summary>
public sealed partial class AnalysisScopeViewModel : ObservableObject
{
    /// <summary>The most found seeds one analysis looks at.</summary>
    public const int MaxSeeds = 1000;

    private const int DefaultSize = 2048;
    private const int MaxSize = 1_000_000;
    private const int MaxCoordinate = 30_000_000;

    public string SeedsLabel { get; } = "Seeds";
    public string CenterLabel { get; } = "Around X and Z";
    public string UseMapCenterLabel { get; } = "Map centre";
    public string ShapeLabel { get; } = "Area";
    public string SizeLabel { get; } = "Side or radius, in blocks";
    public decimal MinCoordinate { get; } = -MaxCoordinate;
    public decimal MaxCoordinateValue { get; } = MaxCoordinate;
    public decimal MinSize { get; } = 1;
    public decimal MaxSizeValue { get; } = MaxSize;

    [ObservableProperty]
    public partial ComboChoice<AnalysisSeeds>? SelectedSeeds { get; set; }

    [ObservableProperty]
    public partial decimal? CenterX { get; set; } = 0;

    [ObservableProperty]
    public partial decimal? CenterZ { get; set; } = 0;

    [ObservableProperty]
    public partial ComboChoice<AreaShape>? SelectedShape { get; set; }

    [ObservableProperty]
    public partial decimal? Size { get; set; } = DefaultSize;

    public ObservableCollection<ComboChoice<AnalysisSeeds>> SeedChoices { get; } = [];

    public ObservableCollection<ComboChoice<AreaShape>> Shapes { get; } =
    [
        new() { Display = "Square", Value = AreaShape.Square },
        new() { Display = "Circle", Value = AreaShape.Circle },
    ];

    public SearchArea Area => SelectedShape?.Value == AreaShape.Circle ? SearchArea.Circle(SizeInBlocks) : SearchArea.Square(SizeInBlocks);

    public BlockPos Reference => new(ToBlock(CenterX), ToBlock(CenterZ));

    private int SizeInBlocks => (int)Math.Clamp(Math.Round(Size ?? DefaultSize), 1, MaxSize);

    private readonly WorldSession _session;
    private readonly MapViewModel _map;
    private readonly ISeedSearchService _search;
    private int _shownFound = -1;

    public AnalysisScopeViewModel(WorldSession session, MapViewModel map, ISeedSearchService search)
    {
        _session = session;
        _map = map;
        _search = search;
        SelectedShape = Shapes[0];
        ShowSeedChoices();
        search.Changed += (_, _) => ShowSeedChoices();
    }

    /// <summary>The worlds to analyse in a dimension: the one on the map, or the first found seeds in the version they were found in.</summary>
    public IReadOnlyList<WorldKey> GetWorlds(Dimension dimension)
    {
        if (SelectedSeeds?.Value != AnalysisSeeds.FoundSeeds || _search.Setup is not { } setup)
            return [_session.Key with { Dimension = dimension }];

        var flags = setup.Definition.Flags;
        return [.. _search.Results.Take(MaxSeeds).Select(seed => new WorldKey(seed, setup.Definition.Version, dimension, flags))];
    }

    [RelayCommand]
    private void UseMapCenter()
    {
        CenterX = ToBlock((decimal)_map.Camera.CenterX);
        CenterZ = ToBlock((decimal)_map.Camera.CenterZ);
    }

    /// <summary>Offers the found seeds while there are some, keeping the choice where it can; nothing changes while their number stays.</summary>
    private void ShowSeedChoices()
    {
        var found = _search.Results.Count;
        if (found == _shownFound)
            return;

        _shownFound = found;
        var wanted = SelectedSeeds?.Value ?? AnalysisSeeds.ThisSeed;
        SeedChoices.Clear();
        SeedChoices.Add(new ComboChoice<AnalysisSeeds> { Display = "The seed on the map", Value = AnalysisSeeds.ThisSeed });

        if (found > 0)
        {
            var count = Math.Min(found, MaxSeeds).ToString("N0", CultureInfo.InvariantCulture);
            SeedChoices.Add(new ComboChoice<AnalysisSeeds> { Display = found > MaxSeeds ? $"The first {count} found seeds" : $"The found seeds ({count})", Value = AnalysisSeeds.FoundSeeds });
        }

        SelectedSeeds = SeedChoices.FirstOrDefault(choice => choice.Value == wanted) ?? SeedChoices[0];
    }

    private static int ToBlock(decimal? value) => (int)Math.Clamp(Math.Round(value ?? 0), -MaxCoordinate, MaxCoordinate);
}

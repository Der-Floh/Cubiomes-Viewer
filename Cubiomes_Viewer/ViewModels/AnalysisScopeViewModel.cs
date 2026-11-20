using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Engine.Service.TileServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Which seeds the analyses look at; they all look at the part of the world the map shows, in its dimension.</summary>
public sealed partial class AnalysisScopeViewModel : ObservableObject
{
    /// <summary>The most found seeds one analysis looks at.</summary>
    public const int MaxSeeds = 1000;

    public string SeedsLabel { get; } = "Seeds";
    public string Hint { get; } = "The analyses look at what the map shows; zoom out to look further.";
    public string HintDetails { get; } = "They look in the dimension the map shows, and look at each found seed in the same place.";

    [ObservableProperty]
    public partial ComboChoice<SeedScope>? SelectedSeeds { get; set; }

    public ObservableCollection<ComboChoice<SeedScope>> SeedChoices { get; } = [];

    private readonly WorldSession _session;
    private readonly MapViewModel _map;
    private readonly ISeedSearchService _search;
    private int _shownFound = -1;

    public AnalysisScopeViewModel(WorldSession session, MapViewModel map, ISeedSearchService search)
    {
        _session = session;
        _map = map;
        _search = search;
        ShowSeedChoices();
        search.Changed += (_, _) => ShowSeedChoices();
    }

    /// <summary>The worlds to analyse in a dimension: the one on the map, or the first found seeds in the version they were found in.</summary>
    public IReadOnlyList<WorldKey> GetWorlds(Dimension dimension)
    {
        if (SelectedSeeds?.Value != SeedScope.FoundSeeds || _search.Setup is not { } setup)
            return [_session.Key with { Dimension = dimension }];

        return [.. _search.Results.Take(MaxSeeds).Select(seed => setup.Definition.GetWorld(seed, dimension))];
    }

    /// <summary>The blocks the map shows now, inside the world border, around the middle of the view.</summary>
    /// <exception cref="InvalidOperationException">The map has no size yet.</exception>
    public AnalysisView TakeView()
    {
        var viewport = _map.Viewport;
        if (viewport.IsEmpty)
            throw new InvalidOperationException("The map shows nothing yet to look at.");

        var reference = new BlockPos(ToBlock(viewport.Camera.CenterX), ToBlock(viewport.Camera.CenterZ));
        var area = SearchArea.Rectangle(
            ToBlock(Math.Floor(viewport.Left)) - reference.X,
            ToBlock(Math.Floor(viewport.Top)) - reference.Z,
            ToBlock(Math.Ceiling(viewport.Right) - 1) - reference.X,
            ToBlock(Math.Ceiling(viewport.Bottom) - 1) - reference.Z);
        return new AnalysisView(area, reference, _session.Dimension);
    }

    /// <summary>Offers the found seeds while there are some, keeping the choice where it can; nothing changes while their number stays.</summary>
    private void ShowSeedChoices()
    {
        var found = _search.Results.Count;
        if (found == _shownFound)
            return;

        _shownFound = found;
        var wanted = SelectedSeeds?.Value ?? SeedScope.ThisSeed;
        SeedChoices.Clear();
        SeedChoices.Add(new ComboChoice<SeedScope> { Display = "The seed on the map", Value = SeedScope.ThisSeed });

        if (found > 0)
        {
            var count = Math.Min(found, MaxSeeds).ToString("N0", CultureInfo.InvariantCulture);
            SeedChoices.Add(new ComboChoice<SeedScope> { Display = found > MaxSeeds ? $"The first {count} found seeds" : $"The found seeds ({count})", Value = SeedScope.FoundSeeds });
        }

        SelectedSeeds = SeedChoices.FirstOrDefault(choice => choice.Value == wanted) ?? SeedChoices[0];
    }

    private static int ToBlock(double value) => (int)Math.Clamp(Math.Round(value), -TilePyramid.WorldLimitBlocks, TilePyramid.WorldLimitBlocks);
}

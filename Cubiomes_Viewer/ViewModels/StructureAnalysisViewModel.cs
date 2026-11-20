using System.Collections.ObjectModel;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.AnalysisServ;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Service.CsvServ;
using Cubiomes_Viewer.Service.MapStyleServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.ProgressServ;
using Cubiomes_Viewer.Service.StorageServ;
using Cubiomes_Viewer.Service.StructureDescriptionServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>
/// The structures of the chosen types in what the map shows, nearest first, and the quads of hut-like structures. Only the
/// types of the dimension the map shows are offered.
/// </summary>
public sealed partial class StructureAnalysisViewModel : AnalysisTabViewModel
{
    public override string Title { get; } = "Structures";
    public override string RunLabel { get; } = "List structures";
    public string TypesLabel { get; } = "Structures";
    public string DescribeLabel { get; } = "With their details";
    public string QuadsLabel { get; } = "Also list quads of";
    public string QuadQualityLabel { get; } = "At least as close as";

    [ObservableProperty]
    public partial bool Describe { get; set; } = true;

    [ObservableProperty]
    public partial bool ListQuads { get; set; }

    [ObservableProperty]
    public partial ComboChoice<StructureType>? SelectedQuadType { get; set; }

    [ObservableProperty]
    public partial ComboChoice<QuadQuality>? SelectedQuadQuality { get; set; }

    /// <summary>The structure types of the version and dimension the map shows, the ones on the map chosen to begin with.</summary>
    public ObservableCollection<MarkerToggleViewModel> Types { get; } = [];

    public IReadOnlyList<ComboChoice<StructureType>> QuadTypes { get; }
    public IReadOnlyList<ComboChoice<QuadQuality>> QuadQualities => QuadChoices.Qualities;

    protected override string FileName { get; } = "structures.csv";

    private readonly IAnalysisService _analysis;
    private readonly IStructureCatalog _structureCatalog;
    private readonly IMapStyleService _styleService;
    private readonly IStructureDescriptionService _descriptions;
    private readonly WorldSession _session;
    private readonly MapViewModel _map;
    private readonly HashSet<StructureType> _chosen = [];
    private bool _isChosenHere;

    public StructureAnalysisViewModel(AnalysisScopeViewModel scope, IAnalysisService analysis, IStructureCatalog structureCatalog, IMapStyleService styleService, IStructureDescriptionService descriptions, WorldSession session, MapViewModel map, ISeedHistoryService seedHistory, IStorageService storage, ICsvService csv, ISettingsService settingsService, IProgressService progress, INotificationService notifications)
        : base(scope, session, map, seedHistory, storage, csv, settingsService, progress, notifications)
    {
        _analysis = analysis;
        _structureCatalog = structureCatalog;
        _styleService = styleService;
        _descriptions = descriptions;
        _session = session;
        _map = map;
        QuadTypes = [.. QuadChoices.CreateTypeChoices(structureCatalog)];
        SelectedQuadType = QuadTypes[0];
        SelectedQuadQuality = QuadQualities[^1];
        ChooseTheMapsStructures();
        session.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName is nameof(WorldSession.Version) or nameof(WorldSession.Dimension))
                ShowTypes();
        };
        map.PropertyChanged += (_, args) =>
        {
            if (args.PropertyName == nameof(MapViewModel.VisibleStructures) && !_isChosenHere)
                ChooseTheMapsStructures();
        };
    }

    protected override async Task<AnalysisResult> AnalyseAsync(AnalysisView view, IProgress<double> progress, CancellationToken cancellationToken)
    {
        var worlds = Scope.GetWorlds(Dimension.Overworld);
        var (area, reference) = (view.Area, view.Reference);
        StructureListRequest[] structures = [.. worlds.SelectMany(world => ChosenTypesOf(world.Version, view).Select(type => new StructureListRequest(world, type, area, reference, Describe)))];
        QuadListRequest[] quads = ListQuads && SelectedQuadType is { } quadType && view.Dimension == Dimension.Overworld
            ? [.. worlds.Where(world => _analysis.FindQuadProblems(quadType.Value, world.Version, area).Count == 0).Select(world => new QuadListRequest(world, quadType.Value, area, reference, SelectedQuadQuality?.Value ?? QuadQuality.Barely))]
            : [];
        if (structures.Length == 0 && quads.Length == 0)
            throw new InvalidOperationException("Choose at least one structure of this version and dimension; zoom in if the map shows too much for it.");

        var listings = await _analysis.ListStructuresAsync(structures, progress, cancellationToken);
        var quadListings = await _analysis.ListQuadsAsync(quads, cancellationToken: cancellationToken);
        List<(WorldKey World, AnalysisRow Row, string[] Fields)> entries = [.. CreateEntries(structures, listings), .. CreateEntries(quads, quadListings)];
        var multipleSeeds = worlds.Count > 1;

        var table = new CsvTable("Seed", "Structure", "X", "Z", "Distance", "Details");
        foreach (var entry in entries)
            table.Add(entry.Fields);
        return new AnalysisResult([.. entries.Select(entry => multipleSeeds ? entry.Row with { Title = Format($"{entry.Row.Title} · seed {entry.World.Seed}") } : entry.Row)], table, Format($"Found {SearchProgressText.Count(entries.Count, "structure")} in {SearchProgressText.Count(worlds.Count, "seed")}."));
    }

    private IEnumerable<StructureType> ChosenTypesOf(McVersion version, AnalysisView view)
    {
        var available = _structureCatalog.GetAvailable(version, view.Dimension).Select(info => info.Type).ToHashSet();
        return _chosen.Where(type => available.Contains(type) && _analysis.FindStructureProblems(type, version, view.Area).Count == 0).Order();
    }

    /// <summary>The structures of each world, nearest first over all types.</summary>
    private IEnumerable<(WorldKey, AnalysisRow, string[])> CreateEntries(IReadOnlyList<StructureListRequest> requests, IReadOnlyList<StructureListing> listings)
        => requests.Zip(listings)
            .SelectMany(pair => pair.Second.Entries.Select(entry => (Request: pair.First, pair.Second.Dimension, Entry: entry)))
            .GroupBy(item => item.Request.World)
            .SelectMany(group => group.OrderBy(item => item.Entry.Position.SquaredDistanceTo(item.Request.Reference)))
            .Select(item => CreateEntry(item.Request, item.Dimension, item.Entry));

    /// <summary>A structure as a line that opens the dimension the structure generates in.</summary>
    private (WorldKey, AnalysisRow, string[]) CreateEntry(StructureListRequest request, Dimension dimension, StructureEntry entry)
    {
        var name = _structureCatalog.GetInfo(request.Type).Name;
        var details = entry.Details is { } known ? string.Join(", ", _descriptions.Describe(known, request.World.Version)) : string.Empty;
        var position = DescribePosition(entry.Position, request.Reference);
        var pick = MapSelection.ForStructure(name, _styleService.GetStructureMarker(request.Type), entry.Position, request.Type, BlockPosText.ToDistance(request.Reference, entry.Position));
        var row = new AnalysisRow(name, details.Length > 0 ? $"{position} · {details}" : position, request.World with { Dimension = dimension }, entry.Position, pick);
        return (request.World, row, [Format($"{request.World.Seed}"), name, Format($"{entry.Position.X}"), Format($"{entry.Position.Z}"), Format($"{entry.Position.DistanceTo(request.Reference):0.#}"), details]);
    }

    private IEnumerable<(WorldKey, AnalysisRow, string[])> CreateEntries(IReadOnlyList<QuadListRequest> requests, IReadOnlyList<QuadListing> listings)
        => requests.Zip(listings).SelectMany(pair => pair.Second.Entries.Select(entry => CreateEntry(pair.First, entry)));

    private (WorldKey, AnalysisRow, string[]) CreateEntry(QuadListRequest request, QuadEntry entry)
    {
        var name = Format($"Quad of {_structureCatalog.GetInfo(request.Type).Name.ToLowerInvariant()}s");
        var quality = QuadChoices.Qualities.FirstOrDefault(choice => choice.Value == entry.Quality)?.Display ?? entry.Quality.ToString();
        var details = Format($"{quality} · {entry.Generating} of 4 generate");
        var row = new AnalysisRow(name, $"middle {DescribePosition(entry.Middle, request.Reference)} · {details}", request.World, entry.Middle);
        return (request.World, row, [Format($"{request.World.Seed}"), name, Format($"{entry.Middle.X}"), Format($"{entry.Middle.Z}"), Format($"{entry.Middle.DistanceTo(request.Reference):0.#}"), details]);
    }

    private void ShowTypes()
    {
        Types.Clear();
        foreach (var info in _structureCatalog.GetAvailable(_session.Version, _session.Dimension))
            Types.Add(new MarkerToggleViewModel(info.Name, _styleService.GetStructureMarker(info.Type), _chosen.Contains(info.Type), isChosen => Choose(info.Type, isChosen)));
    }

    /// <summary>Chooses the structures the map shows, which load after this tab is made, until types are chosen here.</summary>
    private void ChooseTheMapsStructures()
    {
        _chosen.Clear();
        _chosen.UnionWith(_map.VisibleStructures.Count > 0 ? _map.VisibleStructures : [StructureType.Village]);
        ShowTypes();
    }

    private void Choose(StructureType type, bool isChosen)
    {
        _isChosenHere = true;
        if (isChosen)
            _chosen.Add(type);
        else
            _chosen.Remove(type);
    }
}

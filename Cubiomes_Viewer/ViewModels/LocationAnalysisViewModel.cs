using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.AnalysisServ;
using Cubiomes_Viewer.Engine.Service.ConditionServ;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Service.CsvServ;
using Cubiomes_Viewer.Service.MapStyleServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.ProgressServ;
using Cubiomes_Viewer.Service.StorageServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>
/// The spawn point and the strongholds the map shows, and where the conditions of the search hold for each seed. Conditions
/// that look around the world origin in an area look in what the map shows instead.
/// </summary>
public sealed partial class LocationAnalysisViewModel : AnalysisTabViewModel
{
    public override string Title { get; } = "Locations";
    public override string RunLabel { get; } = "Find locations";
    public string LandmarksLabel { get; } = "Spawn point and strongholds";
    public string ConditionsLabel { get; } = "Where the search conditions hold";
    public string Hint { get; } = "Lists the spawn point, the strongholds the map shows and where the search conditions hold.";
    public string HintDetails { get; } = "While the map shows the Overworld, the spawn point is listed wherever it is and the strongholds the map shows, nearest to its middle first. The conditions of the Search panel are tested in full: those around the world origin look in what the map shows, those relative to another keep their own area around it, and those limited by findings look as far as they need to.";

    [ObservableProperty]
    public partial bool ShowLandmarks { get; set; } = true;

    [ObservableProperty]
    public partial bool ShowConditions { get; set; } = true;

    protected override string FileName { get; } = "locations.csv";

    private readonly IAnalysisService _analysis;
    private readonly ISearchSessionService _searchSession;
    private readonly ISeedSearchService _search;
    private readonly IConditionRegistry _conditionRegistry;
    private readonly IStructureCatalog _structureCatalog;
    private readonly IMapStyleService _styleService;

    public LocationAnalysisViewModel(AnalysisScopeViewModel scope, IAnalysisService analysis, ISearchSessionService searchSession, ISeedSearchService search, IConditionRegistry conditionRegistry, IStructureCatalog structureCatalog, IMapStyleService styleService, WorldSession session, MapViewModel map, ISeedHistoryService seedHistory, IStorageService storage, ICsvService csv, ISettingsService settingsService, IProgressService progress, INotificationService notifications)
        : base(scope, session, map, seedHistory, storage, csv, settingsService, progress, notifications)
    {
        _analysis = analysis;
        _searchSession = searchSession;
        _search = search;
        _conditionRegistry = conditionRegistry;
        _structureCatalog = structureCatalog;
        _styleService = styleService;
    }

    protected override async Task<AnalysisResult> AnalyseAsync(AnalysisView view, IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (!ShowLandmarks && !ShowConditions)
            throw new InvalidOperationException("Choose what to look for.");

        var worlds = Scope.GetWorlds(Dimension.Overworld);
        var definition = InView(GetDefinition(), view);
        var landmarks = ShowLandmarks && view.Dimension == Dimension.Overworld ? await _analysis.FindLandmarksAsync(worlds, progress, cancellationToken) : [];
        var locations = ShowConditions && definition.Conditions.Count > 0
            ? await _analysis.FindConditionLocationsAsync(definition, [.. worlds.Select(world => world.Seed)], cancellationToken: cancellationToken)
            : [];

        var table = new CsvTable("Seed", "Location", "X", "Z", "Distance", "Meets the conditions");
        List<AnalysisRow> rows = [];
        for (var index = 0; index < worlds.Count; index++)
        {
            var world = worlds[index];
            if (landmarks.Count > 0)
                AddLandmarks(world, landmarks[index], view, rows, table);
            if (locations.Count > 0)
                AddConditions(world, definition, locations[index], view.Reference, rows, table);
        }

        var multipleSeeds = worlds.Count > 1;
        return new AnalysisResult([.. rows.Select(row => multipleSeeds ? row with { Title = Format($"{row.Title} · seed {row.World.Seed}") } : row)], table, DescribeResult(worlds, definition, locations));
    }

    /// <summary>The definition the seeds belong to: the one that found them, or the one set up for the map's version.</summary>
    private SearchDefinition GetDefinition()
        => Scope.SelectedSeeds?.Value == SeedScope.FoundSeeds && _search.Setup is { } setup ? setup.Definition : _searchSession.Setup.Definition;

    /// <summary>
    /// The definition with the conditions that look around the world origin in an area, in the dimension the map shows,
    /// looking in what the map shows instead.
    /// </summary>
    private SearchDefinition InView(SearchDefinition definition, AnalysisView view)
    {
        var conditions = definition.Conditions.ToDictionary(condition => condition.Id);
        return definition with
        {
            Conditions = [.. definition.Conditions.Select(condition => IsAroundTheOrigin(condition, conditions) && LooksInAnArea(condition) && LooksIn(condition, definition.Version) == view.Dimension
                ? condition with { Area = view.AroundOrigin }
                : condition)],
        };
    }

    /// <summary>Whether a condition looks around the world origin: it is relative to no other, or only to "any of" groups that look around it themselves.</summary>
    private bool IsAroundTheOrigin(SearchCondition condition, IReadOnlyDictionary<int, SearchCondition> conditions)
    {
        for (var step = 0; step <= conditions.Count; step++)
        {
            if (condition.RelativeTo is not { } parent)
                return true;
            if (!conditions.TryGetValue(parent, out var group) || !_conditionRegistry.GetDescriptor(group).AcceptsAnyChild)
                return false;
            condition = group;
        }

        return false;
    }

    private bool LooksInAnArea(SearchCondition condition) => condition.Limit == SearchLimit.Area && _conditionRegistry.GetDescriptor(condition).HasArea;

    private Dimension LooksIn(SearchCondition condition, McVersion version) => _conditionRegistry.GetEvaluator(condition).GetDimension(condition, version);

    private void AddLandmarks(WorldKey world, WorldLandmarks landmarks, AnalysisView view, List<AnalysisRow> rows, CsvTable table)
    {
        if (landmarks.Spawn is { } spawn)
            Add(world, MapSelection.SpawnName, spawn, null, view.Reference, MapSelection.ForSpawn(_styleService.Spawn, spawn), rows, table);

        var shown = landmarks.Strongholds
            .Select((position, index) => (Position: position, Number: index + 1))
            .Where(stronghold => view.Contains(stronghold.Position))
            .OrderBy(stronghold => stronghold.Position.SquaredDistanceTo(view.Reference));
        foreach (var (position, number) in shown)
            Add(world, Format($"{MapSelection.StrongholdName} {number}"), position, null, view.Reference, MapSelection.ForStronghold(number - 1, _styleService.Stronghold, position, BlockPosText.ToDistance(view.Reference, position)), rows, table);
    }

    private void AddConditions(WorldKey world, SearchDefinition definition, ConditionLocations locations, BlockPos reference, List<AnalysisRow> rows, CsvTable table)
    {
        var conditions = definition.Conditions.ToDictionary(condition => condition.Id);
        foreach (var place in locations.Places.OrderBy(place => place.ConditionId).ThenBy(place => place.Position.SquaredDistanceTo(reference)))
        {
            var condition = conditions[place.ConditionId];
            Add(world with { Dimension = place.Dimension }, Format($"{place.ConditionId} · {Describe(condition)}"), place.Position, locations.IsMatch, reference, Pick(condition, place.Position, reference), rows, table);
        }
    }

    /// <summary>What the map picks at the place of a condition: the structure or stronghold it found; the other kinds mark no single thing.</summary>
    private MapSelection? Pick(SearchCondition condition, BlockPos position, BlockPos reference) => condition switch
    {
        StructureCondition structure => MapSelection.ForStructure(_structureCatalog.GetInfo(structure.Type).Name, _styleService.GetStructureMarker(structure.Type), position, structure.Type, BlockPosText.ToDistance(reference, position)),
        StrongholdCondition => MapSelection.ForStructure(MapSelection.StrongholdName, _styleService.Stronghold, position, StructureType.Stronghold, BlockPosText.ToDistance(reference, position)),
        _ => null,
    };

    private static void Add(WorldKey world, string name, BlockPos position, bool? meetsConditions, BlockPos reference, MapSelection? pick, List<AnalysisRow> rows, CsvTable table)
    {
        var detail = DescribePosition(position, reference) + (meetsConditions == false ? " · the seed does not meet every condition" : string.Empty);
        rows.Add(new AnalysisRow(name, detail, world, position, pick));
        table.Add(Format($"{world.Seed}"), name, Format($"{position.X}"), Format($"{position.Z}"), Format($"{position.DistanceTo(reference):0.#}"), meetsConditions switch { true => "yes", false => "no", null => string.Empty });
    }

    private string Describe(SearchCondition condition) => condition switch
    {
        StructureCondition structure => _structureCatalog.GetInfo(structure.Type).Name,
        QuadHutCondition quad => Format($"Quad of {_structureCatalog.GetInfo(quad.Type).Name.ToLowerInvariant()}s"),
        _ => _conditionRegistry.GetDescriptor(condition).Name,
    };

    private static string DescribeResult(IReadOnlyList<WorldKey> worlds, SearchDefinition definition, IReadOnlyList<ConditionLocations> locations)
    {
        var seeds = SearchProgressText.Count(worlds.Count, "seed");
        if (locations.Count == 0)
            return definition.Conditions.Count == 0 ? Format($"Looked at {seeds}; the search has no conditions to test.") : Format($"Looked at {seeds}.");

        if (locations.Count == 1)
            return locations[0].IsMatch ? "The seed meets the conditions of the search." : "The seed does not meet the conditions of the search.";

        return Format($"{locations.Count(location => location.IsMatch):N0} of {seeds} meet the conditions of the search.");
    }
}

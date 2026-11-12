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
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.StorageServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>The spawn point, the strongholds in the area, and where the conditions of the search hold for each seed.</summary>
public sealed partial class LocationAnalysisViewModel : AnalysisTabViewModel
{
    public override string Title { get; } = "Locations";
    public override string RunLabel { get; } = "Find locations";
    public string LandmarksLabel { get; } = "Spawn point and strongholds";
    public string ConditionsLabel { get; } = "Where the search conditions hold";
    public string Hint { get; } = "The spawn point is listed wherever it is, the strongholds only inside the area. The conditions of the Search panel are tested in full; one relative to another shows what it found around the place that made the seed match.";

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

    public LocationAnalysisViewModel(AnalysisScopeViewModel scope, IAnalysisService analysis, ISearchSessionService searchSession, ISeedSearchService search, IConditionRegistry conditionRegistry, IStructureCatalog structureCatalog, WorldSession session, MapViewModel map, ISeedHistoryService seedHistory, IStorageService storage, INotificationService notifications)
        : base(scope, session, map, seedHistory, storage, notifications)
    {
        _analysis = analysis;
        _searchSession = searchSession;
        _search = search;
        _conditionRegistry = conditionRegistry;
        _structureCatalog = structureCatalog;
    }

    protected override async Task<AnalysisResult> AnalyseAsync(IProgress<double> progress, CancellationToken cancellationToken)
    {
        if (!ShowLandmarks && !ShowConditions)
            throw new InvalidOperationException("Choose what to look for.");

        var worlds = Scope.GetWorlds(Dimension.Overworld);
        var definition = GetDefinition();
        var landmarks = ShowLandmarks ? await _analysis.FindLandmarksAsync(worlds, progress, cancellationToken) : [];
        var locations = ShowConditions && definition.Conditions.Count > 0
            ? await _analysis.FindConditionLocationsAsync(definition, [.. worlds.Select(world => world.Seed)], cancellationToken: cancellationToken)
            : [];

        var table = new CsvTable("Seed", "Location", "X", "Z", "Distance", "Meets the conditions");
        List<AnalysisRow> rows = [];
        for (var index = 0; index < worlds.Count; index++)
        {
            var world = worlds[index];
            if (landmarks.Count > 0)
                AddLandmarks(world, landmarks[index], rows, table);
            if (locations.Count > 0)
                AddConditions(world, definition, locations[index], rows, table);
        }

        var multipleSeeds = worlds.Count > 1;
        return new AnalysisResult([.. rows.Select(row => multipleSeeds ? row with { Title = Format($"{row.Title} · seed {row.World.Seed}") } : row)], table, DescribeResult(worlds, definition, locations));
    }

    /// <summary>The definition the seeds belong to: the one that found them, or the one set up for the map's version.</summary>
    private SearchDefinition GetDefinition()
        => Scope.SelectedSeeds?.Value == AnalysisSeeds.FoundSeeds && _search.Setup is { } setup ? setup.Definition : _searchSession.Setup.Definition;

    private void AddLandmarks(WorldKey world, WorldLandmarks landmarks, List<AnalysisRow> rows, CsvTable table)
    {
        if (landmarks.Spawn is { } spawn)
            Add(world, "World spawn", spawn, null, rows, table);

        for (var index = 0; index < landmarks.Strongholds.Length; index++)
        {
            if (Scope.Area.Contains(Scope.Reference, landmarks.Strongholds[index]))
                Add(world, Format($"Stronghold {index + 1}"), landmarks.Strongholds[index], null, rows, table);
        }
    }

    private void AddConditions(WorldKey world, SearchDefinition definition, ConditionLocations locations, List<AnalysisRow> rows, CsvTable table)
    {
        var conditions = definition.Conditions.ToDictionary(condition => condition.Id);
        foreach (var place in locations.Places.OrderBy(place => place.ConditionId).ThenBy(place => place.Position.SquaredDistanceTo(Scope.Reference)))
            Add(world, Format($"{place.ConditionId} · {Describe(conditions[place.ConditionId])}"), place.Position, locations.IsMatch, rows, table);
    }

    private void Add(WorldKey world, string name, BlockPos position, bool? meetsConditions, List<AnalysisRow> rows, CsvTable table)
    {
        var detail = DescribePosition(position, Scope.Reference) + (meetsConditions == false ? " · the seed does not meet every condition" : string.Empty);
        rows.Add(new AnalysisRow(name, detail, world, position));
        table.Add(Format($"{world.Seed}"), name, Format($"{position.X}"), Format($"{position.Z}"), Format($"{position.DistanceTo(Scope.Reference):0.#}"), meetsConditions switch { true => "yes", false => "no", null => string.Empty });
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

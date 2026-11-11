using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Service.ClipboardServ;
using Cubiomes_Viewer.Service.MapStyleServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.StructureDescriptionServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class SelectionViewModelTests : IDisposable
{
    private const int SearchSide = 8192;
    private static readonly BlockPos _spawn = new(-544, -432);
    private static readonly BlockArea _searchArea = new(-SearchSide / 2, -SearchSide / 2, SearchSide, SearchSide);
    private static readonly JobPriority _priority = new(JobPriorityClass.Interactive);

    private readonly ServiceProvider _services = TestServices.Create();
    private readonly SelectionViewModel _selection;
    private readonly MapViewModel _map;
    private readonly WorldSession _session;
    private readonly IMapStyleService _styleService;
    private readonly IComputeService _computeService;

    public SelectionViewModelTests()
    {
        _selection = _services.GetRequiredService<SelectionViewModel>();
        _map = _services.GetRequiredService<MapViewModel>();
        _session = _services.GetRequiredService<WorldSession>();
        _styleService = _services.GetRequiredService<IMapStyleService>();
        _computeService = _services.GetRequiredService<IComputeService>();
        _session.Seed = 262;
    }

    public void Dispose() => _services.Dispose();

    [AvaloniaFact]
    public void Selection_Landmark_OpensTheCardWithItsNameAndPosition()
    {
        _map.Selection = new MapSelection("World spawn", _styleService.Spawn, _spawn);

        Assert.True(_selection.IsOpen);
        Assert.Equal("World spawn", _selection.Title);
        Assert.Same(_styleService.Spawn, _selection.Marker);
        Assert.Equal("X -544, Z -432  ·  Chunk -34, -27", _selection.PositionText);
        Assert.False(_selection.IsLoading);
        Assert.Empty(_selection.Details);
        Assert.Empty(_selection.Boxes);
    }

    [AvaloniaFact]
    public async Task Selection_EndCity_LoadsItsDetailsAndTheBoxesOfItsPieces()
    {
        _session.Dimension = Dimension.End;
        var position = await FindFirstAsync(StructureType.EndCity);
        var expected = await _computeService.RunAsync(new StructureDetailsJob(_map.World, StructureType.EndCity, position), _priority, TestContext.Current.CancellationToken);

        _map.Selection = CreateStructureSelection(StructureType.EndCity, position);
        Assert.True(_selection.IsLoading);
        await TestWait.UntilAsync(() => !_selection.IsLoading, "The details of the end city were not read in time.");

        var description = _services.GetRequiredService<IStructureDescriptionService>().Describe(expected, _map.World.Version);
        Assert.NotEmpty(expected.Pieces);
        Assert.Equal(description, _selection.Details);
        Assert.Contains(_selection.Details, line => line.EndsWith("end ship", StringComparison.Ordinal));
        Assert.Equal(expected.Pieces.Select(piece => piece.Box), _selection.Boxes.TakeLast(expected.Pieces.Length));
        Assert.Empty(GetErrors());
    }

    [AvaloniaFact]
    public async Task Selection_ChangedWhileDetailsLoad_ShowsOnlyTheLatestMarker()
    {
        _session.Dimension = Dimension.End;
        var position = await FindFirstAsync(StructureType.EndCity);

        _map.Selection = CreateStructureSelection(StructureType.EndCity, position);
        _map.Selection = new MapSelection("End gateway 1", _styleService.GetStructureMarker(StructureType.EndGateway), new BlockPos(96, 0));
        await TestWait.UntilAsync(() => !_selection.IsLoading, "The replaced selection kept loading.");
        await Task.Delay(100, TestContext.Current.CancellationToken);

        Assert.Equal("End gateway 1", _selection.Title);
        Assert.Empty(_selection.Details);
        Assert.Empty(_selection.Boxes);
        Assert.Empty(GetErrors());
    }

    [AvaloniaFact]
    public void Close_OpenCard_ClearsTheSelection()
    {
        _map.Selection = new MapSelection("World spawn", _styleService.Spawn, _spawn);

        _selection.CloseCommand.Execute(null);

        Assert.Null(_map.Selection);
        Assert.False(_selection.IsOpen);
    }

    [AvaloniaFact]
    public void World_Changed_ClearsTheSelection()
    {
        _map.Selection = new MapSelection("World spawn", _styleService.Spawn, _spawn);

        _session.Seed = 263;

        Assert.Null(_map.Selection);
        Assert.False(_selection.IsOpen);
    }

    [AvaloniaFact]
    public void SampleHeight_Changed_KeepsTheSelection()
    {
        _map.Selection = new MapSelection("World spawn", _styleService.Spawn, _spawn);

        _session.SampleHeight += 16;

        Assert.NotNull(_map.Selection);
    }

    [AvaloniaFact]
    public async Task Actions_OnAPickedMarker_CenterTheMapAndCopyItsPosition()
    {
        _map.Selection = new MapSelection("World spawn", _styleService.Spawn, _spawn);

        _selection.CenterCommand.Execute(null);
        await _selection.CopyCoordinatesCommand.ExecuteAsync(null);
        await _selection.CopyTeleportCommand.ExecuteAsync(null);

        var clipboard = (RecordingClipboardService)_services.GetRequiredService<IClipboardService>();
        Assert.Equal((_spawn.X, _spawn.Z), (_map.Camera.CenterX, _map.Camera.CenterZ));
        Assert.Equal(["-544, -432", "/tp @s -544 ~ -432"], clipboard.Texts);
        Assert.Empty(GetErrors());
    }

    private MapSelection CreateStructureSelection(StructureType type, BlockPos position)
        => new(type.ToString(), _styleService.GetStructureMarker(type), position, type);

    private async Task<BlockPos> FindFirstAsync(StructureType type)
    {
        var request = new StructureAreaRequest(_map.World, type, _searchArea);
        var found = await _computeService.RunAsync(new StructureAreaJob(request), _priority, TestContext.Current.CancellationToken);
        return found.Positions[0];
    }

    private IEnumerable<string?> GetErrors()
        => ((RecordingNotificationService)_services.GetRequiredService<INotificationService>()).Errors;
}

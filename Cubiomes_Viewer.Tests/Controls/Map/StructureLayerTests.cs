using System.Collections.Frozen;
using System.Diagnostics.CodeAnalysis;

using Avalonia;
using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Controls.Map;
using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Service.MapStyleServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.Controls.Map;

public sealed class StructureLayerTests
{
    private const int ViewSize = 256;
    private const int SampleHeight = 63;
    private const ulong WithRoom = 1;
    private const ulong WithoutRoom = 2;

    private static readonly WorldKey _world = new(262, McVersion.V1_21_3, Dimension.Nether, GeneratorFlags.None);
    private static readonly BlockPos _fortressWithRoom = new(0, 0);
    private static readonly BlockPos _fortressWithoutRoom = new(64, 32);
    private static readonly MapScene _scene = new(_world, MapContent.For(_world, LayerMode.Biomes, SampleHeight, octaves: 0), new MapViewport(new MapCamera(0, 0, 1), ViewSize, ViewSize), PixelGrid.Unscaled);

    [AvaloniaFact]
    public void HitTest_StructureWithAHiddenOption_IsPassedOver()
    {
        var layer = CreateLayer(hiddenOptions: WithoutRoom);

        Assert.Null(layer.HitTest(_scene, ToScreen(_fortressWithoutRoom)));
        Assert.Equal(_fortressWithRoom, layer.HitTest(_scene, ToScreen(_fortressWithRoom))?.Position);
    }

    [AvaloniaFact]
    public void HitTest_NoHiddenOptions_FindsEveryStructure()
    {
        var layer = CreateLayer(hiddenOptions: 0);

        Assert.Equal(_fortressWithoutRoom, layer.HitTest(_scene, ToScreen(_fortressWithoutRoom))?.Position);
        Assert.Equal(_fortressWithRoom, layer.HitTest(_scene, ToScreen(_fortressWithRoom))?.Position);
        Assert.Null(layer.HitTestGroup(_scene, ToScreen(_fortressWithRoom)));
    }

    [AvaloniaFact]
    public void HitTest_ZoomedOutUntilTheMarkersOverlap_FindsTheirGroupInsteadOfEitherOne()
    {
        var far = _scene with { Viewport = new MapViewport(new MapCamera(0, 0, 8), ViewSize, ViewSize) };
        var layer = CreateLayer(hiddenOptions: 0, far);
        var middle = ToScreen(new BlockPos(32, 16), far);

        var group = layer.HitTestGroup(far, middle);

        Assert.Null(layer.HitTest(far, middle));
        Assert.Equal((2, _fortressWithRoom, _fortressWithoutRoom), (group?.Count, group?.Min, group?.Max));
    }

    [AvaloniaFact]
    public void HitTest_GroupWithAHiddenOption_LeavesTheHiddenStructureOut()
    {
        var far = _scene with { Viewport = new MapViewport(new MapCamera(0, 0, 8), ViewSize, ViewSize) };
        var layer = CreateLayer(hiddenOptions: WithoutRoom, far);

        Assert.Null(layer.HitTestGroup(far, ToScreen(_fortressWithRoom, far)));
        Assert.Equal(_fortressWithRoom, layer.HitTest(far, ToScreen(_fortressWithRoom, far))?.Position);
    }

    private static StructureLayer CreateLayer(ulong hiddenOptions) => CreateLayer(hiddenOptions, _scene);

    private static StructureLayer CreateLayer(ulong hiddenOptions, MapScene scene)
    {
        var request = new StructureAreaRequest(_world, StructureType.Fortress, new BlockArea(-ViewSize, -ViewSize, 2 * ViewSize, 2 * ViewSize));
        var structures = new StructureAreaData { Request = request, Positions = [_fortressWithRoom, _fortressWithoutRoom], Options = [WithRoom, WithoutRoom] };
        var layer = new StructureLayer(new FixedAreaService(structures), App.Services.GetRequiredService<IStructureCatalog>(), App.Services.GetRequiredService<IMapStyleService>())
        {
            VisibleTypes = FrozenSet.Create(StructureType.Fortress),
            HiddenOptions = new Dictionary<StructureType, ulong> { [StructureType.Fortress] = hiddenOptions }.ToFrozenDictionary(),
        };
        layer.UpdateScene(scene);
        return layer;
    }

    private static Point ToScreen(BlockPos position) => ToScreen(position, _scene);

    private static Point ToScreen(BlockPos position, MapScene scene)
    {
        var (x, y) = scene.Viewport.ToScreen(position.X, position.Z);
        return new Point(x, y);
    }

    private sealed class FixedAreaService(StructureAreaData structures) : IStructureAreaService
    {
        public event Action? StateChanged
        {
            add { }
            remove { }
        }

        public int PendingCount => 0;

        public IReadOnlyList<StructureAreaRequest> GetVisibleAreas(WorldKey world, StructureType type, MapViewport viewport)
            => type == structures.Request.Type ? [structures.Request] : [];

        public void SetVisibleAreas(IReadOnlyList<StructureAreaRequest> requests)
        {
        }

        public bool TryGetStructures(StructureAreaRequest request, [NotNullWhen(true)] out StructureAreaData? found)
        {
            found = request == structures.Request ? structures : null;
            return found is not null;
        }
    }
}

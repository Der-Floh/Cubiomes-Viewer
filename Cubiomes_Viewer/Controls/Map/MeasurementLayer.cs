using Avalonia;
using Avalonia.Media;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Service.MeasurementServ;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>Draws the measurements kept on the map of the open dimension of the seed, each with its length and name, and picks them.</summary>
public sealed class MeasurementLayer : IMarkerLayer
{
    private static readonly RenderOptions _renderOptions = new() { EdgeMode = EdgeMode.Antialias };

    public event EventHandler? Invalidated;

    public bool IsVisible { get; set; } = true;

    private readonly IMeasurementService _measurementService;
    private readonly MeasurePainter _painter;

    public MeasurementLayer(IMeasurementService measurementService, MeasurePainter painter)
    {
        _measurementService = measurementService;
        _painter = painter;
    }

    public void Attach() => _measurementService.Changed += OnMeasurementsChanged;
    public void Detach() => _measurementService.Changed -= OnMeasurementsChanged;
    public void UpdateScene(MapScene scene) { }

    public void Render(DrawingContext context, MapScene scene)
    {
        if (!IsVisible || scene.Viewport.IsEmpty)
            return;

        using (context.PushRenderOptions(_renderOptions))
        {
            foreach (var measurement in _measurementService.GetMeasurements(scene.World.Seed, scene.World.Dimension))
                _painter.DrawKept(context, scene.Viewport, measurement, isPicked: false);
        }
    }

    public MapSelection? HitTest(MapScene scene, Point position)
    {
        if (!IsVisible || scene.Viewport.IsEmpty)
            return null;

        var measurements = _measurementService.GetMeasurements(scene.World.Seed, scene.World.Dimension);
        for (var index = measurements.Count - 1; index >= 0; index--)
        {
            if (_painter.HitTest(scene.Viewport, measurements[index], position))
                return MapSelection.ForMeasurement(measurements[index]);
        }

        return null;
    }

    private void OnMeasurementsChanged() => Invalidated?.Invoke(this, EventArgs.Empty);
}

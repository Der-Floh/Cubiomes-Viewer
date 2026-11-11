namespace Cubiomes_Viewer.Controls.Map;

/// <summary>The layers one map is drawn with.</summary>
public sealed class MapLayerSet
{
    public TileLayer Tiles { get; }
    public SlimeChunkLayer SlimeChunks { get; }
    public GridLayer Grid { get; }
    public GatewayLayer Gateways { get; }
    public StructureLayer Structures { get; }
    public LandmarkLayer Landmarks { get; }
    public SelectionLayer Selection { get; }
    public MeasureLayer Measure { get; }

    /// <summary>All layers from the bottom one to the top one.</summary>
    public IReadOnlyList<IMapLayer> InDrawOrder => [Tiles, SlimeChunks, Grid, Gateways, Structures, Landmarks, Selection, Measure];

    public MapLayerSet(TileLayer tiles, SlimeChunkLayer slimeChunks, GridLayer grid, GatewayLayer gateways, StructureLayer structures, LandmarkLayer landmarks, SelectionLayer selection, MeasureLayer measure)
    {
        Tiles = tiles;
        SlimeChunks = slimeChunks;
        Grid = grid;
        Gateways = gateways;
        Structures = structures;
        Landmarks = landmarks;
        Selection = selection;
        Measure = measure;
    }
}

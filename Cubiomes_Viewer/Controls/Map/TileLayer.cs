using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Service.TileBitmapServ;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>
/// Draws the tiles of the shown layer at the level that fits the zoom, on top of the next coarser level so the view is covered quickly.
/// </summary>
public sealed class TileLayer : IMapLayer
{
    private const int MaxBitmaps = 512;

    public event EventHandler? Invalidated;

    public bool IsVisible { get; set; } = true;

    /// <summary>How the tiles are painted. Changing it repaints the tiles that are already loaded.</summary>
    public TileStyle Style
    {
        get;
        set
        {
            if (field == value)
                return;

            field = value;
            DropBitmaps();
            Invalidated?.Invoke(this, EventArgs.Empty);
        }
    }

    private readonly IMapTileService _tileService;
    private readonly ITileColorizer _colorizer;
    private readonly ITileBitmapService _bitmapService;
    private readonly Dictionary<TileRequest, LinkedListNode<TileBitmap>> _bitmaps = [];
    private readonly LinkedList<TileBitmap> _recency = new();
    private readonly UiThreadCoalescer _invalidation;
    private byte[] _pixels = [];
    private long _frame;

    /// <summary>
    /// The last content whose tiles covered the view. While the tiles of another height or octave limit of the same layer
    /// load, it is drawn beneath them, so that stepping through heights does not blank the map.
    /// </summary>
    private MapContent? _standIn;

    public TileLayer(IMapTileService tileService, ITileColorizer colorizer, ITileBitmapService bitmapService)
    {
        _tileService = tileService;
        _colorizer = colorizer;
        _bitmapService = bitmapService;
        _invalidation = new UiThreadCoalescer(() => Invalidated?.Invoke(this, EventArgs.Empty));
    }

    public void Attach() => _tileService.StateChanged += _invalidation.Request;

    public void Detach()
    {
        _tileService.StateChanged -= _invalidation.Request;
        _tileService.SetVisibleTiles([]);
        DropBitmaps();
    }

    public void UpdateScene(MapScene scene)
    {
        if (!IsVisible || scene.Viewport.IsEmpty)
        {
            _tileService.SetVisibleTiles([]);
            return;
        }

        var metrics = TileMetrics.For(scene.Content.World, scene.Content.Layer);
        var fineScale = TilePyramid.SelectScale(metrics, scene.Viewport.BlocksPerPixel);
        TileRequest[] fine = [.. GetRequests(scene.Content, scene, metrics, fineScale)];
        TileRequest[] coarse = TilePyramid.GetCoarserScale(metrics, fineScale) is { } coarseScale ? [.. GetRequests(scene.Content, scene, metrics, coarseScale)] : [];

        _tileService.SetVisibleTiles([.. coarse, .. fine, .. GetReliefRequests([.. fine, .. coarse])]);
    }

    public void Render(DrawingContext context, MapScene scene)
    {
        if (!IsVisible || scene.Viewport.IsEmpty)
            return;

        _frame++;
        var metrics = TileMetrics.For(scene.Content.World, scene.Content.Layer);
        var fineScale = TilePyramid.SelectScale(metrics, scene.Viewport.BlocksPerPixel);
        var coarseScale = TilePyramid.GetCoarserScale(metrics, fineScale);

        if (_standIn is { } standIn && standIn != scene.Content && IsVariantOf(standIn, scene.Content))
            DrawContent(context, scene, standIn, metrics, fineScale, coarseScale);

        if (DrawContent(context, scene, scene.Content, metrics, fineScale, coarseScale))
            _standIn = scene.Content;
    }

    private static IEnumerable<TileRequest> GetRequests(MapContent content, MapScene scene, TileMetrics metrics, int scale)
        => TilePyramid.GetVisibleTiles(metrics, scale, scene.Viewport)
            .Select(content.GetRequest);

    /// <summary>The height tiles that shade the given tiles in the current style, in their order and each once.</summary>
    private IEnumerable<TileRequest> GetReliefRequests(IEnumerable<TileRequest> tiles)
        => tiles.Select(tile => _colorizer.GetReliefTile(tile, Style)).OfType<TileRequest>().Distinct();

    /// <summary>Contents of the same layer of the same world share their tile grid and differ only in height or octave limit.</summary>
    private static bool IsVariantOf(MapContent standIn, MapContent content) => standIn.World == content.World && standIn.Layer == content.Layer;

    /// <summary>Draws the coarser level and then the level that fits the zoom; returns whether the latter covered the view.</summary>
    private bool DrawContent(DrawingContext context, MapScene scene, MapContent content, TileMetrics metrics, int fineScale, int? coarseScale)
    {
        if (coarseScale is { } coarse)
            DrawLevel(context, scene, content, metrics, coarse);
        return DrawLevel(context, scene, content, metrics, fineScale);
    }

    private bool DrawLevel(DrawingContext context, MapScene scene, MapContent content, TileMetrics metrics, int scale)
    {
        var tileBlocks = TilePyramid.GetTileBlocks(metrics, scale);
        var isComplete = true;
        foreach (var request in GetRequests(content, scene, metrics, scale))
        {
            if (GetBitmap(request) is not { } bitmap)
            {
                isComplete = false;
                continue;
            }

            var (left, top) = scene.Viewport.ToScreen(request.Tile.X * tileBlocks, request.Tile.Z * tileBlocks);
            var (right, bottom) = scene.Viewport.ToScreen((request.Tile.X + 1) * tileBlocks, (request.Tile.Z + 1) * tileBlocks);
            context.DrawImage(bitmap, new Rect(
                new Point(scene.Pixels.SnapX(left), scene.Pixels.SnapY(top)),
                new Point(scene.Pixels.SnapX(right), scene.Pixels.SnapY(bottom))));
        }

        return isComplete;
    }

    private Bitmap? GetBitmap(TileRequest request)
    {
        if (_bitmaps.TryGetValue(request, out var node))
        {
            _recency.Remove(node);
            _recency.AddFirst(node);
            node.Value = AddArrivedRelief(node.Value) with { Frame = _frame };
            return node.Value.Bitmap;
        }

        if (!_tileService.TryGetTile(request, out var tile))
            return null;

        var relief = _colorizer.GetReliefTile(request, Style);
        var heights = relief is { } reliefRequest && _tileService.TryGetTile(reliefRequest, out var loaded) ? loaded : null;
        var bitmap = CreateBitmap(tile, heights);
        _bitmaps.Add(request, _recency.AddFirst(new TileBitmap(request, bitmap, _frame, heights is null ? relief : null)));
        TrimBitmaps();
        return bitmap;
    }

    /// <summary>Paints a tile that was painted flat again, with relief, once the heights that shade it have loaded.</summary>
    private TileBitmap AddArrivedRelief(TileBitmap entry)
    {
        if (entry.MissingRelief is not { } relief || !_tileService.TryGetTile(relief, out var heights) || !_tileService.TryGetTile(entry.Request, out var tile))
            return entry;

        entry.Bitmap.Dispose();
        return entry with { Bitmap = CreateBitmap(tile, heights), MissingRelief = null };
    }

    private Bitmap CreateBitmap(TileData tile, TileData? heights)
    {
        var byteCount = tile.Samples * tile.Samples * TileColorizer.BytesPerPixel;
        if (_pixels.Length < byteCount)
            _pixels = new byte[byteCount];

        var pixels = _pixels.AsSpan(0, byteCount);
        _colorizer.Colorize(tile, Style, pixels, heights);
        return _bitmapService.Create(pixels, tile.Samples, tile.Samples);
    }

    private void DropBitmaps()
    {
        foreach (var entry in _recency)
            entry.Bitmap.Dispose();
        _recency.Clear();
        _bitmaps.Clear();
    }

    /// <summary>Lets go of the bitmaps used longest ago beyond the limit, but never of one the frame being drawn uses.</summary>
    private void TrimBitmaps()
    {
        while (_bitmaps.Count > MaxBitmaps && _recency.Last is { } last && last.Value.Frame != _frame)
        {
            _recency.RemoveLast();
            _bitmaps.Remove(last.Value.Request);
            last.Value.Bitmap.Dispose();
        }
    }

    /// <summary>A painted tile; <see cref="MissingRelief"/> names the heights it still waits for when it was painted flat without them.</summary>
    private readonly record struct TileBitmap(TileRequest Request, Bitmap Bitmap, long Frame, TileRequest? MissingRelief = null);
}

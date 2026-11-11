using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;

using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.TileServ;
using Cubiomes_Viewer.Service.MapStyleServ;
using Cubiomes_Viewer.Service.TileBitmapServ;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>Tints the slime chunks of the Overworld while chunks are large enough on screen to be told apart.</summary>
public sealed class SlimeChunkLayer : IMapLayer
{
    private const double MaxBlocksPerPixel = 8;
    private const int MaxBitmaps = 64;

    public event EventHandler? Invalidated;

    public bool IsVisible { get; set; }

    private readonly ISlimeAreaService _slimeService;
    private readonly IMapStyleService _styleService;
    private readonly ITileBitmapService _bitmapService;
    private readonly UiThreadCoalescer _invalidation;
    private readonly Dictionary<SlimeAreaRequest, Bitmap> _bitmaps = [];
    private IReadOnlyList<SlimeAreaRequest> _requests = [];

    public SlimeChunkLayer(ISlimeAreaService slimeService, IMapStyleService styleService, ITileBitmapService bitmapService)
    {
        _slimeService = slimeService;
        _styleService = styleService;
        _bitmapService = bitmapService;
        _invalidation = new UiThreadCoalescer(() => Invalidated?.Invoke(this, EventArgs.Empty));
    }

    public void Attach() => _slimeService.StateChanged += _invalidation.Request;

    public void Detach()
    {
        _slimeService.StateChanged -= _invalidation.Request;
        _requests = [];
        _slimeService.SetVisibleAreas(_requests);
        ClearBitmaps();
    }

    public void UpdateScene(MapScene scene)
    {
        _requests = HasContent(scene) ? _slimeService.GetVisibleAreas(scene.World.Seed, scene.Viewport) : [];
        _slimeService.SetVisibleAreas(_requests);
    }

    public void Render(DrawingContext context, MapScene scene)
    {
        foreach (var request in _requests)
        {
            if (GetBitmap(request) is not { } bitmap)
                continue;

            var (left, top) = scene.Viewport.ToScreen(ToBlocks(request.ChunkX), ToBlocks(request.ChunkZ));
            var (right, bottom) = scene.Viewport.ToScreen(ToBlocks(request.ChunkX + request.SizeChunks), ToBlocks(request.ChunkZ + request.SizeChunks));
            context.DrawImage(bitmap, new Rect(
                new Point(scene.Snap(left), scene.Snap(top)),
                new Point(scene.Snap(right), scene.Snap(bottom))));
        }
    }

    private bool HasContent(MapScene scene)
        => IsVisible && scene.World.Dimension == Dimension.Overworld && !scene.Viewport.IsEmpty && scene.Viewport.BlocksPerPixel <= MaxBlocksPerPixel;

    private static double ToBlocks(int chunk) => (double)chunk * SlimeAreaService.ChunkBlocks;

    private Bitmap? GetBitmap(SlimeAreaRequest request)
    {
        if (_bitmaps.TryGetValue(request, out var cached))
            return cached;

        if (!_slimeService.TryGetSlimeChunks(request, out var slimeChunks))
            return null;

        if (_bitmaps.Count >= MaxBitmaps)
            ClearBitmaps();

        var bitmap = CreateBitmap(slimeChunks);
        _bitmaps.Add(request, bitmap);
        return bitmap;
    }

    private Bitmap CreateBitmap(SlimeAreaData slimeChunks)
    {
        var color = _styleService.SlimeChunkColor;
        var opacity = color.A / (double)byte.MaxValue;
        byte[] slimePixel = [(byte)(color.B * opacity), (byte)(color.G * opacity), (byte)(color.R * opacity), color.A];

        var size = slimeChunks.Request.SizeChunks;
        var pixels = new byte[size * size * TileColorizer.BytesPerPixel];
        for (var z = 0; z < size; z++)
        {
            for (var x = 0; x < size; x++)
            {
                if (slimeChunks.IsSlimeChunk(x, z))
                    slimePixel.CopyTo(pixels, (z * size + x) * TileColorizer.BytesPerPixel);
            }
        }

        return _bitmapService.Create(pixels, size, size);
    }

    private void ClearBitmaps()
    {
        foreach (var bitmap in _bitmaps.Values)
            bitmap.Dispose();
        _bitmaps.Clear();
    }
}

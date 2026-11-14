using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Data;

/// <summary>A view of the map as a link carries it: the world, the layer and the block in the middle, at a zoom of <see cref="BlocksPerPixel"/>.</summary>
public sealed record MapLink(long Seed, McVersion Version, Dimension Dimension, bool LargeBiomes, BlockPos Center, double BlocksPerPixel, LayerMode Layer, int SampleHeight, int Octaves)
{
    private const double BlockMiddle = 0.5;

    /// <summary>The camera that shows the view, centred on the middle of its block, so that the view's own link names the same block again.</summary>
    public MapCamera Camera => new(Center.X + BlockMiddle, Center.Z + BlockMiddle, BlocksPerPixel);

    /// <summary>The view of a session through a camera, named by the block under the middle of the map.</summary>
    public static MapLink Of(WorldSession session, MapCamera camera)
    {
        ArgumentNullException.ThrowIfNull(session);

        var center = new BlockPos((int)Math.Floor(camera.CenterX), (int)Math.Floor(camera.CenterZ));
        return new MapLink(session.Seed, session.Version, session.Dimension, session.LargeBiomes, center, camera.BlocksPerPixel, session.Layer, session.SampleHeight, session.Octaves);
    }
}

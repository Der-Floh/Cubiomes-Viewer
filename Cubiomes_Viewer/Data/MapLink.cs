using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Data;

/// <summary>
/// A view of the map as a link carries it: the world, the layer and the block in the middle, at a zoom of <see cref="BlocksPerPixel"/>,
/// and the pins on the map of its seed and dimension.
/// </summary>
public sealed record MapLink(long Seed, McVersion Version, Dimension Dimension, bool LargeBiomes, BlockPos Center, double BlocksPerPixel, LayerMode Layer, int SampleHeight, int Octaves)
{
    private const double BlockMiddle = 0.5;

    public IReadOnlyList<LinkPin> Pins { get; init; } = [];

    /// <summary>The camera that shows the view, centred on the middle of its block, so that the view's own link names the same block again.</summary>
    public MapCamera Camera => new(Center.X + BlockMiddle, Center.Z + BlockMiddle, BlocksPerPixel);

    /// <summary>The view of a session through a camera, named by the block under the middle of the map, without pins.</summary>
    public static MapLink Of(WorldSession session, MapCamera camera)
    {
        ArgumentNullException.ThrowIfNull(session);

        var center = new BlockPos((int)Math.Floor(camera.CenterX), (int)Math.Floor(camera.CenterZ));
        return new MapLink(session.Seed, session.Version, session.Dimension, session.LargeBiomes, center, camera.BlocksPerPixel, session.Layer, session.SampleHeight, session.Octaves);
    }

    /// <summary>Links are equal when they show the same view with the same pins in the same order.</summary>
    public bool Equals(MapLink? other)
        => other is not null && Seed == other.Seed && Version == other.Version && Dimension == other.Dimension && LargeBiomes == other.LargeBiomes
            && Center == other.Center && BlocksPerPixel.Equals(other.BlocksPerPixel) && Layer == other.Layer && SampleHeight == other.SampleHeight
            && Octaves == other.Octaves && Pins.SequenceEqual(other.Pins);

    public override int GetHashCode()
        => HashCode.Combine(HashCode.Combine(Seed, Version, Dimension, LargeBiomes, Center, BlocksPerPixel, Layer, SampleHeight), Octaves, Pins.Count);
}

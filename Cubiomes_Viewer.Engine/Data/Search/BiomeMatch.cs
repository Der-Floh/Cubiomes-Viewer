namespace Cubiomes_Viewer.Engine.Data.Search;

public enum BiomeMatch
{
    /// <summary>One of the biomes is enough.</summary>
    Any,

    /// <summary>Each of the biomes has to occur.</summary>
    All,

    /// <summary>The biomes together cover a share of the area, estimated from samples at random points.</summary>
    Share,
}

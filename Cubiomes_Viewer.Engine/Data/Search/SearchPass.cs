namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>How much a pass over the conditions may work out; passes run in this order and get more costly.</summary>
public enum SearchPass
{
    /// <summary>
    /// Only what the seed tells without generating biomes: where structures attempt to generate and where slime chunks
    /// are. That depends on the structure seed alone, so all sister seeds come out the same.
    /// </summary>
    Quick = 0,

    /// <summary>Everything, with the biomes of the world.</summary>
    Full = 1,
}

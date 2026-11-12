namespace Cubiomes_Viewer.Engine.Data.Structures;

/// <summary>
/// How close the four structures of a quad are, from best to worst. The values mirror the classes of the native
/// shim. Each class is defined by the lower 20 bits of the quad's base, after the constellations cubiomes lists.
/// </summary>
public enum QuadQuality
{
    /// <summary>The closest constellations there are.</summary>
    Ideal = 1,

    /// <summary>The constellations of the classic quad-hut farms.</summary>
    Classic = 2,

    /// <summary>Close enough for a farm with a 43-block drop below the huts.</summary>
    Normal = 3,

    /// <summary>Only just close enough for the four huts to be in range of one point.</summary>
    Barely = 4,

    /// <summary>In range of one point, but outside the listed constellations.</summary>
    Other = 5,
}

namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>Where in its area a climate condition wants the values in range.</summary>
public enum ClimateMatch
{
    /// <summary>Every sample of the area.</summary>
    Everywhere = 0,

    /// <summary>At least one sample of the area.</summary>
    Somewhere = 1,

    /// <summary>The mean of the samples of the area.</summary>
    OnAverage = 2,
}

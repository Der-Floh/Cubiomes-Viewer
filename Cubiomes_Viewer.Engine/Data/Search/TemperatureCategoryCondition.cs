using Cubiomes_Viewer.Engine.Data.Maps;

namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>
/// Regions of a temperature category in the area, for the layered Overworld of 1.7 to 1.17: each cell of the special
/// layer with the category is a match at its middle. Cells are 1,024 blocks wide, or 4,096 with large biomes.
/// </summary>
public sealed record TemperatureCategoryCondition : SearchCondition
{
    public TemperatureCategory Category { get; init; } = TemperatureCategory.SpecialLush;
}

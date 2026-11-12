using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>
/// Cells of a temperature category in an area, from the special layer of the layered Overworld. The layer needs the
/// whole seed, so the quick pass cannot tell, but it is far cheaper than any biome.
/// </summary>
public sealed class TemperatureCategoryConditionEvaluator : ConditionEvaluator<TemperatureCategoryCondition>
{
    private const McVersion FirstVersion = McVersion.V1_7_10;
    private const McVersion LastVersion = McVersion.V1_17_1;
    private const int CellBlocks = 1024;
    private const int LargeBiomeCellBlocks = 4096;
    private const int MaxMatches = 256;
    private const int MaxCells = 512 * 512;
    private const int SearchCost = 20;
    private const int DefaultSide = 8192;

    public override ConditionDescriptor Descriptor { get; } = new()
    {
        ConditionType = typeof(TemperatureCategoryCondition),
        Name = "Temperature category",
        Description = "Regions of a temperature category of 1.7 to 1.17 lie in the area.",
        Create = id => new TemperatureCategoryCondition { Id = id, Area = SearchArea.Square(DefaultSide) },
        Cost = SearchCost,
        MaxCount = MaxMatches,
    };

    protected override IEnumerable<string> Validate(TemperatureCategoryCondition condition, SearchDefinition definition)
    {
        if (definition.Version is < FirstVersion or > LastVersion)
            yield return "Temperature categories decide the biomes of 1.7 to 1.17 only.";
        if (!Enum.IsDefined(condition.Category))
            yield return "Choose a temperature category.";

        var cellBlocks = GetCellBlocks(definition.Flags);
        foreach (var problem in SampleGrid.FindProblems(condition.Area, cellBlocks, [cellBlocks], MaxCells))
            yield return problem;
    }

    protected override MatchStatus Evaluate(TemperatureCategoryCondition condition, BlockPos reference, SearchSeedContext context, SearchPass pass, List<BlockPos> matches)
    {
        if (pass == SearchPass.Quick)
            return MatchStatus.Unknown;

        var blocks = condition.Area.ToBlocks(reference);
        var grid = SampleGrid.Create(blocks, GetCellBlocks(context.Flags), 0);
        var cells = context.GetBytes(grid.SampleCount);
        context.GetGenerator(Dimension.Overworld).GenerateLayer(GeneratorLayer.TemperatureCategory, grid with { Scale = CellBlocks }, cells);

        for (var index = 0; index < cells.Length && matches.Count < MaxMatches; index++)
        {
            var position = SampleGrid.GetPosition(grid, index, blocks);
            if (cells[index] == (byte)condition.Category && condition.Area.Contains(reference, position))
                matches.Add(position);
        }

        return Decide(condition, reference, matches);
    }

    /// <summary>Large biomes stretch the biomes of each cell of the layer to four times its width.</summary>
    private static int GetCellBlocks(GeneratorFlags flags) => flags.HasFlag(GeneratorFlags.LargeBiomes) ? LargeBiomeCellBlocks : CellBlocks;
}

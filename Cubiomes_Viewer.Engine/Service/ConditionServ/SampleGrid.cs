using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.ConditionServ;

/// <summary>The grid a condition samples the rectangle of its area on, every so many blocks.</summary>
internal static class SampleGrid
{
    private const int VerticalCellShift = 2;

    /// <summary>The samples that cover a rectangle, at a height in blocks.</summary>
    public static BiomeArea Create(BlockArea blocks, int scale, int y)
    {
        var (left, top) = (FloorDivide(blocks.X, scale), FloorDivide(blocks.Z, scale));
        var (right, bottom) = (FloorDivide(blocks.Right - 1, scale), FloorDivide(blocks.Bottom - 1, scale));
        return new BiomeArea(scale, left, top, right - left + 1, bottom - top + 1, scale == 1 ? y : y >> VerticalCellShift);
    }

    /// <summary>The middle of the part of a sample that lies in the rectangle.</summary>
    public static BlockPos GetPosition(BiomeArea grid, int index, BlockArea blocks)
    {
        var (x, z) = (grid.X + index % grid.SizeX, grid.Z + index / grid.SizeX);
        return new BlockPos(
            Math.Clamp(x * grid.Scale + grid.Scale / 2, blocks.X, blocks.Right - 1),
            Math.Clamp(z * grid.Scale + grid.Scale / 2, blocks.Z, blocks.Bottom - 1));
    }

    /// <summary>The most samples an area can need, wherever it lies.</summary>
    public static long CountAtMost(SearchArea area, int scale) => ((long)area.SizeX / scale + 2) * ((long)area.SizeZ / scale + 2);

    /// <summary>What keeps an area from being sampled every so many blocks: a spacing it does not offer, or too many samples.</summary>
    public static IEnumerable<string> FindProblems(SearchArea area, int scale, int[] scales, int maxSamples)
    {
        if (!scales.Contains(scale))
            yield return $"The distance between samples must be one of {string.Join(", ", scales)} blocks.";
        else if (CountAtMost(area, scale) > maxSamples)
            yield return $"The area needs more than {maxSamples} samples at {scale} blocks per sample; sample it more coarsely or make it smaller.";
    }

    private static int FloorDivide(int value, int divisor) => (int)Math.Floor(value / (double)divisor);
}

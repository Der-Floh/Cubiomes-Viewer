using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Controls.Map;

/// <summary>Groups markers that would overlap on screen, so that the map shows their number instead of a pile of them.</summary>
public static class MarkerGrouping
{
    /// <summary>
    /// Groups the positions that lie less than <paramref name="reach"/> blocks apart along both axes, as square markers of that
    /// side would overlap. Each group starts at the first position not grouped yet in a fixed order across the world, north to
    /// south and then west to east, and takes in the positions within reach of it; so the groups stay put while the map moves and
    /// only change with the zoom or with positions coming and going.
    /// </summary>
    public static MarkerGroup[] Group(IReadOnlyList<BlockPos> positions, double reach)
    {
        ArgumentNullException.ThrowIfNull(positions);

        var order = Enumerable.Range(0, positions.Count).ToArray();
        Array.Sort(order, (a, b) => Compare(positions[a], positions[b]));
        if (reach <= 0)
            return [.. order.Select(index => Single(positions, index))];

        var cells = new Dictionary<(long X, long Z), List<int>>();
        foreach (var index in order)
        {
            var cell = GetCell(positions[index], reach);
            if (!cells.TryGetValue(cell, out var members))
                cells.Add(cell, members = []);
            members.Add(index);
        }

        var isGrouped = new bool[positions.Count];
        List<MarkerGroup> groups = [];
        foreach (var index in order)
        {
            if (!isGrouped[index])
                groups.Add(Gather(positions, index, reach, cells, isGrouped));
        }

        return [.. groups];
    }

    private static MarkerGroup Gather(IReadOnlyList<BlockPos> positions, int first, double reach, Dictionary<(long X, long Z), List<int>> cells, bool[] isGrouped)
    {
        var start = positions[first];
        var (cellX, cellZ) = GetCell(start, reach);
        var (count, sumX, sumZ) = (0, 0.0, 0.0);
        var (minX, minZ, maxX, maxZ) = (start.X, start.Z, start.X, start.Z);
        for (var x = cellX - 1; x <= cellX + 1; x++)
        {
            for (var z = cellZ - 1; z <= cellZ + 1; z++)
            {
                if (!cells.TryGetValue((x, z), out var members))
                    continue;

                foreach (var member in members)
                {
                    var position = positions[member];
                    if (isGrouped[member] || Math.Abs(position.X - start.X) >= reach || Math.Abs(position.Z - start.Z) >= reach)
                        continue;

                    isGrouped[member] = true;
                    (count, sumX, sumZ) = (count + 1, sumX + position.X, sumZ + position.Z);
                    (minX, minZ) = (Math.Min(minX, position.X), Math.Min(minZ, position.Z));
                    (maxX, maxZ) = (Math.Max(maxX, position.X), Math.Max(maxZ, position.Z));
                }
            }
        }

        return count == 1
            ? Single(positions, first)
            : new MarkerGroup(sumX / count, sumZ / count, count, first, new BlockPos(minX, minZ), new BlockPos(maxX, maxZ));
    }

    private static MarkerGroup Single(IReadOnlyList<BlockPos> positions, int index)
    {
        var position = positions[index];
        return new MarkerGroup(position.X, position.Z, 1, index, position, position);
    }

    private static (long X, long Z) GetCell(BlockPos position, double reach)
        => ((long)Math.Floor(position.X / reach), (long)Math.Floor(position.Z / reach));

    private static int Compare(BlockPos a, BlockPos b) => a.Z != b.Z ? a.Z.CompareTo(b.Z) : a.X.CompareTo(b.X);
}

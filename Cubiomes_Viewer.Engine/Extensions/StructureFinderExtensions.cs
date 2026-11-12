using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.Engine.Extensions;

public static class StructureFinderExtensions
{
    /// <summary>The structures that make up a quad: one in each of its four regions.</summary>
    public const int StructuresPerQuad = 4;

    extension(IStructureFinder finder)
    {
        /// <summary>
        /// Writes where the four structures of a quad attempt to generate, region by region along x first; returns
        /// <see langword="false"/> when a region holds no attempt.
        /// </summary>
        public bool TryGetQuadPositions(StructureType type, McVersion version, long seed, QuadPosition quad, Span<BlockPos> positions)
        {
            ArgumentNullException.ThrowIfNull(finder);
            ArgumentOutOfRangeException.ThrowIfLessThan(positions.Length, StructuresPerQuad);

            for (var index = 0; index < StructuresPerQuad; index++)
            {
                if (!finder.TryGetRegionPosition(type, version, seed, quad.RegionX + index % 2, quad.RegionZ + index / 2, out positions[index]))
                    return false;
            }

            return true;
        }
    }
}

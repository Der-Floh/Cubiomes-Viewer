using Cubiomes_Viewer.Engine.Data.Structures;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

/// <summary>
/// Knows every quad-hut base. They are found on the compute workers the first time a class is asked for, which
/// takes seconds, and kept in app data afterwards.
/// </summary>
public interface IQuadHutBaseService
{
    /// <summary>
    /// Returns the bases of a class and of every better one: best class first, and within a class grouped by their
    /// lower 20 bits in the order cubiomes lists them, ascending in each group.
    /// </summary>
    /// <param name="worst">The worst class to include, from <see cref="QuadQuality.Ideal"/> to <see cref="QuadQuality.Barely"/>.</param>
    /// <param name="progress">Receives the share of the missing bases found so far, from 0 to 1.</param>
    /// <param name="cancellationToken">Stops looking; groups of bases found completely so far are kept.</param>
    Task<IReadOnlyList<ulong>> GetBasesAsync(QuadQuality worst, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}

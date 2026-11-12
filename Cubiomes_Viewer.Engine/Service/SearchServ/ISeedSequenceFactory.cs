using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.SearchServ;

public interface ISeedSequenceFactory
{
    /// <summary>Lists what keeps a source from being scanned in a version; nothing when it can be.</summary>
    IEnumerable<string> Validate(SeedSource source, McVersion version);

    /// <summary>Prepares the seeds of a valid source, which for quad huts means finding their bases first.</summary>
    /// <param name="source">The seeds to scan.</param>
    /// <param name="definition">The search the seeds are scanned for, which gives the version and the structure rules.</param>
    /// <param name="progress">Receives the share prepared so far, from 0 to 1, when the preparation takes a while.</param>
    /// <param name="cancellationToken">Stops the preparation.</param>
    Task<ISeedSequence> CreateAsync(SeedSource source, SearchDefinition definition, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}

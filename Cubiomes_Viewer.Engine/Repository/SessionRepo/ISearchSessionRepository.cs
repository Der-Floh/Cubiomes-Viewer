using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Repository.SessionRepo;

public interface ISearchSessionRepository
{
    /// <summary>Loads the stored session; a missing or unreadable one yields <see langword="null"/>.</summary>
    Task<SearchSession?> LoadAsync(CancellationToken cancellationToken = default);

    Task SaveAsync(SearchSession session, CancellationToken cancellationToken = default);

    /// <summary>Writes a session as the content of a session file.</summary>
    byte[] Serialize(SearchSession session);

    /// <summary>Reads the content of a session file.</summary>
    /// <exception cref="FormatException">The content is not a session this version can read.</exception>
    SearchSession Deserialize(ReadOnlySpan<byte> content);
}

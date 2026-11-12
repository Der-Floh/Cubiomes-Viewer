using System.Text;

using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Repository.SessionRepo;
using Cubiomes_Viewer.Engine.Tests.Data.Search;

using Microsoft.Extensions.Logging.Abstractions;

namespace Cubiomes_Viewer.Engine.Tests.Repository;

public sealed class SearchSessionRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cubiomes-viewer-tests", Guid.NewGuid().ToString("N"));
    private readonly FileSystemAppDataStore _store;
    private readonly SearchSessionRepository _repository;

    public SearchSessionRepositoryTests()
    {
        _store = new FileSystemAppDataStore(_directory);
        _repository = new SearchSessionRepository(_store, NullLogger<SearchSessionRepository>.Instance);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    [Fact]
    public async Task LoadAsync_NothingStored_ReturnsNothing()
        => Assert.Null(await _repository.LoadAsync(TestContext.Current.CancellationToken));

    [Fact]
    public async Task SaveAsync_ThenLoadAsync_RoundTripsTheSetupAndTheSearch()
    {
        var session = CreateSession();

        await _repository.SaveAsync(session, TestContext.Current.CancellationToken);
        var loaded = await _repository.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(session, loaded);
    }

    [Fact]
    public async Task SaveAsync_SessionWithoutASearch_RoundTripsTheSetup()
    {
        var session = new SearchSession { Setup = CreateSession().Setup };

        await _repository.SaveAsync(session, TestContext.Current.CancellationToken);
        var loaded = await _repository.LoadAsync(TestContext.Current.CancellationToken);

        Assert.Equal(session, loaded);
        Assert.Null(loaded?.Search);
    }

    [Fact]
    public async Task LoadAsync_CorruptContent_ReturnsNothing()
    {
        await _store.WriteAsync(SearchSessionRepository.StorageKey, Encoding.UTF8.GetBytes("{ not json"), TestContext.Current.CancellationToken);

        Assert.Null(await _repository.LoadAsync(TestContext.Current.CancellationToken));
    }

    [Fact]
    public void Serialize_WritesKindsAndNamesInsteadOfNumbers()
    {
        var json = Encoding.UTF8.GetString(_repository.Serialize(CreateSession()));

        Assert.Contains("\"$kind\":\"structure\"", json, StringComparison.Ordinal);
        Assert.Contains("\"type\":\"Village\"", json, StringComparison.Ordinal);
        Assert.Contains("\"version\":\"V1_16_5\"", json, StringComparison.Ordinal);
        Assert.Contains("\"biomes\":[1,129]", json, StringComparison.Ordinal);
        Assert.DoesNotContain("sizeX", json, StringComparison.Ordinal);
        Assert.DoesNotContain("isExclusion", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("null")]
    [InlineData("[]")]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"setup\":{\"definition\":{\"conditions\":[{\"$kind\":\"volcano\",\"id\":1}]}}}")]
    [InlineData("{\"setup\":{\"definition\":{\"conditions\":[{\"id\":1}]}}}")]
    [InlineData("{\"setup\":{\"definition\":{\"conditions\":[{\"$kind\":\"structure\",\"id\":1,\"type\":\"Castle\"}]}}}")]
    public void Deserialize_NotASession_ThrowsFormatException(string json)
        => Assert.Throws<FormatException>(() => _repository.Deserialize(Encoding.UTF8.GetBytes(json)));

    [Fact]
    public void Deserialize_SessionOfANewerVersion_ThrowsFormatException()
    {
        var json = Encoding.UTF8.GetString(_repository.Serialize(new SearchSession { SchemaVersion = SearchSession.CurrentSchemaVersion + 1, Setup = CreateSession().Setup }));

        var exception = Assert.Throws<FormatException>(() => _repository.Deserialize(Encoding.UTF8.GetBytes(json)));

        Assert.Contains("newer version", exception.Message, StringComparison.Ordinal);
    }

    private static SearchSession CreateSession()
    {
        var setup = new SearchSetup { Definition = SampleDefinitions.CreateEveryKindOfCondition(), FirstSeed = -5_000_000_000, LastSeed = 7, MaxResults = 250, Workers = 3 };
        return new SearchSession
        {
            Setup = setup,
            Search = new SearchSnapshot { Setup = setup with { MaxResults = 10 }, NextSeed = -4_999_000_000, Results = [-4_999_999_999, -4_999_500_000] },
        };
    }
}

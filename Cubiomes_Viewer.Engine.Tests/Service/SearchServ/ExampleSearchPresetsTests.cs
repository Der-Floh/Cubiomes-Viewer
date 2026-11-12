using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Repository.AppDataRepo;
using Cubiomes_Viewer.Engine.Service.ConditionServ;
using Cubiomes_Viewer.Engine.Service.SearchServ;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Engine.Tests.Service.SearchServ;

/// <summary>Every example the app comes with has to find seeds, on the real workers.</summary>
public sealed class ExampleSearchPresetsTests : IDisposable
{
    private const long ScannedSeeds = 200_000;

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "cubiomes-viewer-tests", Guid.NewGuid().ToString("N"));
    private readonly ServiceProvider _services;

    public ExampleSearchPresetsTests()
    {
        _services = new ServiceCollection().AddLogging().AddCubiomesEngine().AddSingleton<IAppDataStore>(new FileSystemAppDataStore(_directory)).BuildServiceProvider();
    }

    public void Dispose()
    {
        _services.Dispose();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    public static TheoryData<string> Names() => [.. ExampleSearchPresets.All.Select(preset => preset.Name)];

    public static TheoryData<string, McVersion> NamesAndVersions()
    {
        var data = new TheoryData<string, McVersion>();
        foreach (var preset in ExampleSearchPresets.All)
        {
            foreach (var version in new[] { McVersion.V1_16_5, McVersion.V1_21_3 })
                data.Add(preset.Name, version);
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(NamesAndVersions))]
    public void Example_InRecentVersions_CanBeSearched(string name, McVersion version)
    {
        var setup = InVersion(Find(name).Setup, version);

        var problems = _services.GetRequiredService<IConditionValidator>().Validate(setup.Definition).Select(problem => problem.ToString())
            .Concat(_services.GetRequiredService<ISeedSequenceFactory>().Validate(setup.Source, version));

        Assert.Empty(problems);
    }

    [Theory]
    [MemberData(nameof(Names))]
    public async Task Example_SearchedFromSeedZero_FindsSeeds(string name)
    {
        var preset = Find(name);
        var setup = preset.Setup with
        {
            Source = preset.Setup.Source is SeedRangeSource ? new SeedRangeSource { LastSeed = ScannedSeeds } : preset.Setup.Source,
            MaxResults = 1,
        };
        var search = _services.GetRequiredService<ISeedSearchService>();

        await search.StartAsync(setup, TestContext.Current.CancellationToken);

        Assert.Single(search.Results);
    }

    [Fact]
    public void Examples_EachOne_HasANameAndADescription()
    {
        Assert.All(ExampleSearchPresets.All, preset => Assert.False(string.IsNullOrWhiteSpace(preset.Description)));
        Assert.Equal(ExampleSearchPresets.All.Count, ExampleSearchPresets.All.Select(preset => preset.Name).Distinct(StringComparer.OrdinalIgnoreCase).Count());
    }

    private static SearchPreset Find(string name) => ExampleSearchPresets.All.Single(preset => preset.Name == name);

    private static SearchSetup InVersion(SearchSetup setup, McVersion version) => setup with { Definition = setup.Definition with { Version = version } };
}

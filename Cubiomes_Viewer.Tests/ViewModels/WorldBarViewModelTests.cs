using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class WorldBarViewModelTests : IDisposable
{
    private readonly ServiceProvider _services = TestServices.Create();
    private readonly WorldBarViewModel _viewModel;
    private readonly WorldSession _session;

    public WorldBarViewModelTests()
    {
        _viewModel = _services.GetRequiredService<WorldBarViewModel>();
        _session = _services.GetRequiredService<WorldSession>();
    }

    public void Dispose() => _services.Dispose();

    [AvaloniaFact]
    public void ApplySeed_Number_OpensThatSeedAndRemembersIt()
    {
        _viewModel.SeedText = " 262 ";

        _viewModel.ApplySeedCommand.Execute(null);

        Assert.Equal(262, _session.Seed);
        Assert.Equal("262", _viewModel.SeedText);
        Assert.Equal(string.Empty, _viewModel.SeedHint);
        Assert.Equal([262L], _services.GetRequiredService<ISeedHistoryService>().Seeds);
        Assert.Equal("262", Assert.Single(_viewModel.RecentSeeds).Display);
    }

    [AvaloniaFact]
    public void ApplySeed_Text_HashesItAndShowsWhereTheSeedCameFrom()
    {
        _viewModel.SeedText = "hello";

        _viewModel.ApplySeedCommand.Execute(null);

        Assert.Equal(99162322, _session.Seed);
        Assert.Equal("99162322", _viewModel.SeedText);
        Assert.Equal("from text", _viewModel.SeedHint);
    }

    [AvaloniaFact]
    public void RandomSeed_Always_ShowsTheNewSeedAsNumber()
    {
        _viewModel.RandomSeedCommand.Execute(null);

        Assert.Equal(_session.Seed.ToString(System.Globalization.CultureInfo.InvariantCulture), _viewModel.SeedText);
        Assert.Equal("random", _viewModel.SeedHint);
    }

    [AvaloniaFact]
    public void SelectedVersion_OldVersion_UpdatesSessionAndDisablesLargeBiomes()
    {
        _viewModel.SelectedVersion = _viewModel.Versions.Single(choice => choice.Value == McVersion.V1_2_5);

        Assert.Equal(McVersion.V1_2_5, _session.Version);
        Assert.False(_viewModel.IsLargeBiomesAvailable);
    }

    [AvaloniaFact]
    public void Versions_FromCatalog_AreListedNewestFirst()
    {
        Assert.Equal(McVersion.V1_21_WinterDrop, _viewModel.Versions[0].Value);
        Assert.Equal(McVersion.Beta1_7, _viewModel.Versions[^1].Value);
        Assert.Equal(_session.Version, _viewModel.SelectedVersion?.Value);
    }

    [AvaloniaFact]
    public void SelectedDimensionAndHeight_Changed_UpdateTheSession()
    {
        _viewModel.SelectedDimension = _viewModel.Dimensions.Single(choice => choice.Value == Dimension.Nether);
        _viewModel.SampleHeight = 32;
        _viewModel.LargeBiomes = true;

        Assert.Equal(Dimension.Nether, _session.Dimension);
        Assert.Equal(32, _session.SampleHeight);
        Assert.True(_session.Key.Flags.HasFlag(GeneratorFlags.LargeBiomes));
    }

    [AvaloniaFact]
    public void SelectedRecentSeed_Chosen_ReopensTheSeedAndClearsTheSelection()
    {
        _viewModel.SeedText = "1";
        _viewModel.ApplySeedCommand.Execute(null);
        _viewModel.SeedText = "2";
        _viewModel.ApplySeedCommand.Execute(null);

        _viewModel.SelectedRecentSeed = _viewModel.RecentSeeds.Single(choice => choice.Value == 1);

        Assert.Equal(1, _session.Seed);
        Assert.Null(_viewModel.SelectedRecentSeed);
        Assert.Equal(1, _viewModel.RecentSeeds[0].Value);
    }

    [AvaloniaFact]
    public void GoTo_Coordinates_MovesTheMapCentreAndKeepsTheZoom()
    {
        _viewModel.GoToX = 1234;
        _viewModel.GoToZ = -5678;

        _viewModel.GoToCommand.Execute(null);

        Assert.Equal(new MapCamera(1234, -5678, MapCamera.DefaultBlocksPerPixel), _viewModel.Map.Camera);
    }
}

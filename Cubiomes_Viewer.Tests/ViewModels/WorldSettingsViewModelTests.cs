using Avalonia.Headless.XUnit;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class WorldSettingsViewModelTests : IDisposable
{
    private const int CustomSalt = 12345;

    private readonly ServiceProvider _services = TestServices.Create();
    private readonly WorldSettingsViewModel _viewModel;
    private readonly WorldSession _session;
    private readonly WorldBarViewModel _worldBar;

    public WorldSettingsViewModelTests()
    {
        _session = _services.GetRequiredService<WorldSession>();
        _session.Version = McVersion.V1_21_3;
        _viewModel = _services.GetRequiredService<WorldSettingsViewModel>();
        _worldBar = _services.GetRequiredService<WorldBarViewModel>();
        _viewModel.ShowSettings();
    }

    private StructureSaltViewModel Village => _viewModel.Salts.Single(salt => salt.Type == StructureType.Village);
    private ISettingsService Settings => _services.GetRequiredService<ISettingsService>();

    public void Dispose() => _services.Dispose();

    [AvaloniaFact]
    public void Salts_ListOnlyTheTypesWhosePositionsDependOnOne_StartingWithTheGamesSalt()
    {
        Assert.DoesNotContain(_viewModel.Salts, salt => salt.Type == StructureType.Mineshaft);
        Assert.Equal(10387312, Village.Salt);
        Assert.False(Village.IsReplacing);
        Assert.Contains(_viewModel.Salts, salt => salt.Name == "Ruined portal (Nether)");
        Assert.Same(StructureRules.Default, _session.Rules);
    }

    [AvaloniaFact]
    public void ReplacedSalt_ReachesTheWorldTheMapAndTheSearchShow()
    {
        Village.Salt = CustomSalt;
        Village.IsReplacing = true;

        var expected = new StructureRules { Salts = [new StructureSalt(StructureType.Village, CustomSalt)] };
        Assert.Equal(expected, _session.Rules);
        Assert.Equal(expected, _services.GetRequiredService<MapViewModel>().World.Rules);
        Assert.Equal(expected, _services.GetRequiredService<SearchSetupViewModel>().Setup.Definition.Rules);
        Assert.True(_viewModel.HasReplacedSalts);
    }

    [AvaloniaFact]
    public void ReplacementSwitchedOff_KeepsTheEnteredSaltForLater()
    {
        Village.IsReplacing = true;
        Village.Salt = CustomSalt;

        Village.IsReplacing = false;

        Assert.Same(StructureRules.Default, _session.Rules);
        var stored = Assert.Single(Settings.Current.World.Salts);
        Assert.Equal((StructureType.Village, CustomSalt, false), (stored.Type, stored.Salt, stored.IsReplacing));
    }

    [AvaloniaFact]
    public void StoredSalts_AreShownAgainInAnotherSession()
    {
        Village.IsReplacing = true;
        Village.Salt = CustomSalt;
        using var other = TestServices.Create();
        var otherSettings = other.GetRequiredService<ISettingsService>();
        otherSettings.Current.World = Settings.Current.World;
        var otherWorld = other.GetRequiredService<WorldSettingsViewModel>();

        otherWorld.ShowSettings();

        var village = otherWorld.Salts.Single(salt => salt.Type == StructureType.Village);
        Assert.Equal((true, (decimal?)CustomSalt), (village.IsReplacing, village.Salt));
        Assert.Equal(_session.Rules, other.GetRequiredService<WorldSession>().Rules);
    }

    [AvaloniaFact]
    public void UseGameSalts_SwitchesEveryReplacementOff()
    {
        Village.IsReplacing = true;
        _viewModel.Salts.Single(salt => salt.Type == StructureType.SwampHut).IsReplacing = true;

        _viewModel.UseGameSaltsCommand.Execute(null);

        Assert.All(_viewModel.Salts, salt => Assert.False(salt.IsReplacing));
        Assert.Same(StructureRules.Default, _session.Rules);
        Assert.False(_viewModel.HasReplacedSalts);
    }

    [AvaloniaFact]
    public void TerrainEstimateSwitchedOff_ReachesTheWorld()
    {
        _viewModel.EstimateTerrain = false;

        Assert.False(_session.Rules.EstimateTerrain);
        Assert.False(Settings.Current.World.EstimateTerrain);
    }

    [AvaloniaFact]
    public void ExperimentalVersions_AreOfferedOnlyOnceSwitchedOn()
    {
        var before = _worldBar.Versions.Select(choice => choice.Value).ToList();

        _viewModel.ShowExperimentalVersions = true;

        Assert.DoesNotContain(McVersion.V1_21_WinterDrop, before);
        Assert.DoesNotContain(McVersion.Beta1_7, before);
        Assert.Contains(McVersion.V1_21_WinterDrop, _worldBar.Versions.Select(choice => choice.Value));
        Assert.Equal(McVersion.V1_21_WinterDrop, _worldBar.Versions[0].Value);
        Assert.Equal(McVersion.V1_21_3, _worldBar.SelectedVersion?.Value);
    }

    [AvaloniaFact]
    public void ExperimentalVersionOfTheOpenWorld_StaysOfferedWhileTheyAreHidden()
    {
        _session.Version = McVersion.Beta1_8;

        Assert.Contains(McVersion.Beta1_8, _worldBar.Versions.Select(choice => choice.Value));
        Assert.DoesNotContain(McVersion.Beta1_7, _worldBar.Versions.Select(choice => choice.Value));
        Assert.Equal(McVersion.Beta1_8, _worldBar.SelectedVersion?.Value);
    }
}

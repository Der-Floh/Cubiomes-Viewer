using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.Headless.XUnit;
using Avalonia.Input;
using Avalonia.Threading;
using Avalonia.VisualTree;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.ViewModels;
using Cubiomes_Viewer.Views;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.Views;

public sealed class SearchPanelViewTests : IDisposable
{
    private const int PanelWidth = 320;
    private const int PanelHeight = 2400;
    private const int ManySlimeChunks = 14;
    private const string ConditionHeaderClass = "condition-header";
    private const string RemoveButtonClass = "card-close";
    private const string ResultsClass = "results";

    private readonly ServiceProvider _services = TestServices.Create();
    private readonly SearchPanelViewModel _panel;
    private readonly Window _window;

    public SearchPanelViewTests()
    {
        _panel = _services.GetRequiredService<SearchPanelViewModel>();
        var view = _services.GetRequiredService<SearchPanelView>();
        view.DataContext = _panel;
        _window = new Window { Width = PanelWidth, Height = PanelHeight, Content = view };
        _window.Show();
    }

    public void Dispose()
    {
        _window.Close();
        _services.Dispose();
    }

    [AvaloniaFact]
    public void AddConditionMenu_ItemClicked_AddsThatKindAndTheMenuStaysReadyForTheNext()
    {
        var addButton = FindAll<DropDownButton>().Single(button => Equals(button.Content, _panel.Setup.AddConditionLabel));

        var kinds = HeadlessInput.OpenMenu(addButton);
        Click(kinds.Single(item => Equals(item.Header, "Slime chunks")));
        var afterFirst = (Count: _panel.Setup.Conditions.Count, IsOpen: addButton.Flyout?.IsOpen);
        Click(HeadlessInput.OpenMenu(addButton).Single(item => Equals(item.Header, "Biome")));

        Assert.Equal(_panel.Setup.ConditionKinds.Select(kind => kind.Name), kinds.Select(item => item.Header));
        Assert.Equal((1, false), (afterFirst.Count, afterFirst.IsOpen));
        Assert.Equal(["1 · Slime chunks", "2 · Biome"], _panel.Setup.Conditions.Select(condition => condition.Header));
        Assert.Equal(_panel.Setup.AddConditionLabel, addButton.Content);
    }

    [AvaloniaFact]
    public void ExamplesMenu_ItemClicked_OpensThatExample()
    {
        var examples = FindAll<DropDownButton>().Single(button => Equals(button.Content, _panel.Presets.ExamplesLabel));

        Click(HeadlessInput.OpenMenu(examples).Single(item => Equals(item.Header, "Slime chunks at the origin")));

        Assert.Equal("1 · Slime chunks", Assert.Single(_panel.Setup.Conditions).Header);
        Assert.False(examples.Flyout?.IsOpen);
    }

    [AvaloniaFact]
    public void ConditionHeader_Clicked_FoldsTheEditorAwayAndBack()
    {
        var condition = Add<StructureCondition>();
        var fields = Assert.IsType<StructureFieldsViewModel>(condition.Fields);
        var header = FindAll<ToggleButton>().Single(button => button.Classes.Contains(ConditionHeaderClass));
        var typeChoice = FindAll<ComboBox>().Single(box => ReferenceEquals(box.ItemsSource, fields.Types));
        Assert.True(typeChoice.IsEffectivelyVisible);

        Click(header);
        var folded = (condition.IsExpanded, typeChoice.IsEffectivelyVisible);
        Click(header);

        Assert.Equal((false, false), folded);
        Assert.True(condition.IsExpanded);
        Assert.True(typeChoice.IsEffectivelyVisible);
    }

    [AvaloniaFact]
    public void KindChoice_ShownOnlyForStructuresThatComeInKinds()
    {
        var fields = Assert.IsType<StructureFieldsViewModel>(Add<StructureCondition>().Fields);

        fields.SelectedType = fields.Types.Single(choice => choice.Value == StructureType.Village);
        Render();
        var forVillages = IsKindChoiceShown(fields);
        fields.SelectedType = fields.Types.Single(choice => choice.Value == StructureType.DesertPyramid);
        Render();

        Assert.True(forVillages);
        Assert.False(IsKindChoiceShown(fields));
    }

    [AvaloniaFact]
    public void BiomeEditors_ShowTheBiomesToPick()
    {
        var biome = Assert.IsType<BiomeFieldsViewModel>(Add<BiomeCondition>().Fields);
        var patch = Assert.IsType<BiomePatchFieldsViewModel>(Add<BiomePatchCondition>().Fields);

        var shown = FindAll<CheckBox>().Select(box => box.DataContext).OfType<BiomeToggleViewModel>().ToHashSet();

        Assert.Contains(biome.Picker.Biomes.First(), shown);
        Assert.Contains(patch.Picker.Biomes.First(), shown);
    }

    [AvaloniaFact]
    public void BiomeShare_ShowsItsSamplesInsteadOfTheGrid()
    {
        var fields = Assert.IsType<BiomeFieldsViewModel>(Add<BiomeCondition>().Fields);
        var onGrid = (Shown(fields, fields.ScaleLabel), Shown(fields, fields.SamplesLabel));

        fields.SelectedMatch = fields.Matches.Single(choice => choice.Value == BiomeMatch.Share);
        Render();

        Assert.Equal((true, false), onGrid);
        Assert.Equal((false, true), (Shown(fields, fields.ScaleLabel), Shown(fields, fields.SamplesLabel)));
    }

    [AvaloniaFact]
    public void ClimateHeight_ShownOnlyForTheDepth()
    {
        _services.GetRequiredService<WorldSession>().Version = McVersion.V1_21_3;
        var fields = Assert.IsType<ClimateFieldsViewModel>(Add<ClimateCondition>().Fields);
        var withoutHeight = IsHeightShown(fields);

        fields.SelectedParameter = fields.Parameters.Single(choice => choice.Value == ClimateParameter.Depth);
        Render();

        Assert.False(withoutHeight);
        Assert.True(IsHeightShown(fields));
    }

    [AvaloniaFact]
    public void RemoveButton_Clicked_RemovesTheCondition()
    {
        Add<StructureCondition>();

        Click(FindAll<Button>().Single(button => button.Classes.Contains(RemoveButtonClass)));

        Assert.Empty(_panel.Setup.Conditions);
        Assert.DoesNotContain(FindAll<ToggleButton>(), button => button.Classes.Contains(ConditionHeaderClass));
    }

    [AvaloniaFact]
    public async Task SearchButton_Clicked_FindsSeedsAndAClickOnOneOpensItOnTheMap()
    {
        Add<SlimeChunkCondition>().Count = ManySlimeChunks;
        _panel.Setup.Seeds.LastSeedText = "199999";
        _panel.Setup.MaxResults = 3;
        var search = _services.GetRequiredService<ISeedSearchService>();

        Click(FindAll<Button>().Single(button => Equals(button.Content, _panel.Run.StartLabel)));
        await TestWait.UntilAsync(() => search.State == SearchState.Finished, "The search did not finish.");
        Render();
        var seeds = FindAll<ListBox>().Single(box => box.Classes.Contains(ResultsClass)).GetVisualDescendants().OfType<ListBoxItem>().ToList();
        Click(seeds[1]);

        Assert.Equal(3, seeds.Count);
        Assert.Equal(search.Results[1], _services.GetRequiredService<WorldSession>().Seed);
        Assert.Same(_panel.Results.Seeds[1], _panel.Results.SelectedSeed);
    }

    private SearchConditionViewModel Add<TCondition>() where TCondition : SearchCondition
    {
        _panel.Setup.ConditionKinds.Single(kind => kind.Descriptor.ConditionType == typeof(TCondition)).AddCommand.Execute(null);
        Render();
        return _panel.Setup.Conditions[^1];
    }

    private bool IsKindChoiceShown(StructureFieldsViewModel fields) => FindAll<ComboBox>().Any(box => ReferenceEquals(box.ItemsSource, fields.Traits));

    private bool IsHeightShown(ClimateFieldsViewModel fields) => Shown(fields, fields.HeightLabel);

    private bool Shown(object fields, string label) => FindAll<TextBlock>().Any(text => text.DataContext == fields && text.Text == label);

    private static void Click(Control control) => HeadlessInput.Click(control);

    private static void Render() => HeadlessInput.Render();

    private IEnumerable<T> FindAll<T>() where T : Control
        => _window.GetVisualDescendants().OfType<T>().Where(control => control.IsEffectivelyVisible);
}

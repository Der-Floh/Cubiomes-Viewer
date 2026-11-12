using Avalonia.Headless.XUnit;
using Avalonia.Media;
using Avalonia.Threading;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Colors;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ColorMapServ;
using Cubiomes_Viewer.Engine.Service.SettingsServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.StorageServ;
using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer.Tests.ViewModels;

public sealed class BiomeColorsViewModelTests : IDisposable
{
    private const int Plains = 1;

    private static readonly RgbColor _pink = new(0xff, 0x66, 0xcc);

    private readonly ServiceProvider _services = TestServices.Create();
    private readonly BiomeColorsViewModel _colors;
    private readonly MapViewModel _map;

    public BiomeColorsViewModelTests()
    {
        _services.GetRequiredService<WorldSession>().Version = McVersion.V1_21_3;
        _colors = _services.GetRequiredService<BiomeColorsViewModel>();
        _map = _services.GetRequiredService<MapViewModel>();
    }

    private IColorMapService ColorMaps => _services.GetRequiredService<IColorMapService>();
    private MemoryStorageService Storage => (MemoryStorageService)_services.GetRequiredService<IStorageService>();
    private RecordingNotificationService Notifications => (RecordingNotificationService)_services.GetRequiredService<INotificationService>();

    public void Dispose() => _services.Dispose();

    [AvaloniaFact]
    public async Task NewMap_EditedAndSaved_IsStoredChosenAndPaintedWith()
    {
        _colors.NewCommand.Execute(null);
        SetPlains(_pink);
        Dispatcher.UIThread.RunJobs();
        var preview = _map.BiomeColors;

        await _colors.SaveCommand.ExecuteAsync(null);

        Assert.Equal(_pink, preview?[Plains]);
        Assert.Empty(Notifications.Errors);
        var stored = Assert.Single(ColorMaps.Maps);
        Assert.Equal("My colours", stored.Name);
        Assert.Equal([new BiomeColor(Plains, _pink)], stored.Colors);
        Assert.Equal("My colours", _colors.SelectedMap?.Value);
        Assert.Equal(_pink, _map.BiomeColors?[Plains]);
        Assert.Equal("My colours", _services.GetRequiredService<ISettingsService>().Current.Map.ColorMap);
        Assert.False(_colors.IsEditing);
    }

    [AvaloniaFact]
    public void Cancel_DropsTheEditsAndPaintsWithTheChosenColoursAgain()
    {
        _colors.NewCommand.Execute(null);
        SetPlains(_pink);
        Dispatcher.UIThread.RunJobs();

        _colors.CancelCommand.Execute(null);

        Assert.Null(_map.BiomeColors);
        Assert.Empty(ColorMaps.Maps);
        Assert.False(_colors.IsEditing);
    }

    [AvaloniaFact]
    public async Task Edit_OfAStoredMap_StartsFromItsColoursAndKeepsItsName()
    {
        await ColorMaps.SaveAsync(new BiomeColorMap { Name = "Pastel", Colors = [new BiomeColor(Plains, _pink)] });
        await _colors.LoadAsync();
        _colors.SelectedMap = _colors.MapChoices.Single(choice => choice.Value == "Pastel");

        _colors.EditCommand.Execute(null);

        Assert.Equal("Pastel", _colors.EditName);
        Assert.Equal(_pink, _colors.Entries.Single(entry => entry.Biome == Plains).RgbColor);
        Assert.True(_colors.Entries.Single(entry => entry.Biome == Plains).IsChanged);
    }

    [AvaloniaFact]
    public async Task Save_UnderTheNameOfAnotherMap_IsRefused()
    {
        await ColorMaps.SaveAsync(new BiomeColorMap { Name = "Pastel" });
        await _colors.LoadAsync();
        _colors.NewCommand.Execute(null);
        _colors.EditName = "pastel";

        await _colors.SaveCommand.ExecuteAsync(null);

        Assert.Contains("already", Assert.Single(Notifications.Errors), StringComparison.Ordinal);
        Assert.True(_colors.IsEditing);
        Assert.Single(ColorMaps.Maps);
    }

    [AvaloniaFact]
    public async Task Load_ChoosesTheMapChosenLast()
    {
        await ColorMaps.SaveAsync(new BiomeColorMap { Name = "Pastel", Colors = [new BiomeColor(Plains, _pink)] });
        _services.GetRequiredService<ISettingsService>().Current.Map.ColorMap = "Pastel";

        await _colors.LoadAsync();

        Assert.Equal("Pastel", _colors.SelectedMap?.Value);
        Assert.Equal(_pink, _map.BiomeColors?[Plains]);
    }

    [AvaloniaFact]
    public async Task Delete_OfTheChosenMap_GoesBackToTheDefaultColours()
    {
        await ColorMaps.SaveAsync(new BiomeColorMap { Name = "Pastel", Colors = [new BiomeColor(Plains, _pink)] });
        await _colors.LoadAsync();
        _colors.SelectedMap = _colors.MapChoices.Single(choice => choice.Value == "Pastel");

        await _colors.DeleteCommand.ExecuteAsync(null);

        Assert.Empty(ColorMaps.Maps);
        Assert.Null(_colors.SelectedMap?.Value);
        Assert.Null(_map.BiomeColors);
        Assert.False(_colors.DeleteCommand.CanExecute(null));
    }

    [AvaloniaFact]
    public async Task ExportedMap_ImportedAgain_ArrivesUnderAFreeNameAndIsChosen()
    {
        await ColorMaps.SaveAsync(new BiomeColorMap { Name = "Pastel", Colors = [new BiomeColor(Plains, _pink)] });
        await _colors.LoadAsync();
        _colors.SelectedMap = _colors.MapChoices.Single(choice => choice.Value == "Pastel");
        await _colors.ExportCommand.ExecuteAsync(null);
        Storage.ContentToOpen = Storage.SavedContent;

        await _colors.ImportCommand.ExecuteAsync(null);

        Assert.Equal("Pastel.json", Storage.LastSaveOptions?.SuggestedFileName);
        Assert.Equal("Pastel 2", _colors.SelectedMap?.Value);
        Assert.Equal(_pink, _map.BiomeColors?[Plains]);
        Assert.Empty(Notifications.Errors);
    }

    [AvaloniaFact]
    public void Filter_ListsOnlyTheBiomesWhoseNameHasIt()
    {
        _colors.NewCommand.Execute(null);
        var all = _colors.Entries.Count;

        _colors.Filter = "desert";

        Assert.True(all > 50);
        Assert.NotEmpty(_colors.Entries);
        Assert.All(_colors.Entries, entry => Assert.Contains("desert", entry.Name, StringComparison.OrdinalIgnoreCase));
    }

    [AvaloniaFact]
    public void AllDimmed_DimsEveryBiomeOnTheMapAtOnce()
    {
        _colors.NewCommand.Execute(null);

        _colors.UseAllDimmedCommand.Execute(null);

        var plains = _colors.Entries.Single(entry => entry.Biome == Plains);
        Assert.Equal(plains.DefaultColor.ToDimmed(), _map.BiomeColors?[Plains]);
        Assert.All(_colors.Entries, entry => Assert.Equal(entry.DefaultColor.ToDimmed(), entry.RgbColor));
    }

    private void SetPlains(RgbColor color)
        => _colors.Entries.Single(entry => entry.Biome == Plains).Color = Color.FromRgb(color.Red, color.Green, color.Blue);
}

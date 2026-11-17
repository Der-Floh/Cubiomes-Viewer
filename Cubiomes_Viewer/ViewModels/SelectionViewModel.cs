using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.MeasurementServ;
using Cubiomes_Viewer.Engine.Service.PinServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Service.MapStyleServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.StructureDescriptionServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>
/// Describes what is picked on the map: what it is, where it lies and, for structures, what the generator tells of it.
/// A picked pin can be given another look or be taken away.
/// </summary>
public sealed partial class SelectionViewModel : ViewModelBase
{
    private static readonly JobPriority _priority = new(JobPriorityClass.Interactive);

    public string CloseLabel { get; } = "Close";
    public string LoadingLabel { get; } = "Reading details...";
    public string PinLookLabel { get; } = "Change the pin's icon";
    public string PinNameLabel { get; } = "Name the pin";
    public string MeasurementNameLabel { get; } = "Name the measurement";
    public string RemovePinLabel { get; } = "Remove pin";
    public string RemoveMeasurementLabel { get; } = "Remove measurement";
    public string PlainPinLabel { get; } = "Plain pin";
    public string SpawnPinLabel { get; } = "World spawn";
    public string StrongholdPinLabel { get; } = "Stronghold";

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial MapMarker? Marker { get; set; }

    [ObservableProperty]
    public partial string PositionText { get; set; } = string.Empty;

    /// <summary>More about the pick, such as where a spot came from; empty when there is nothing to add.</summary>
    [ObservableProperty]
    public partial string Note { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNamable), nameof(NamePlaceholder), nameof(NameLabel), nameof(RemoveLabel))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    public partial bool IsPin { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsNamable), nameof(NamePlaceholder), nameof(NameLabel), nameof(RemoveLabel))]
    [NotifyCanExecuteChangedFor(nameof(RemoveCommand))]
    public partial bool IsMeasurement { get; set; }

    /// <summary>Whether the pick is one of the user's own, a pin or a kept measurement, which can be named and taken away.</summary>
    public bool IsNamable => IsPin || IsMeasurement;

    public string NamePlaceholder => IsMeasurement ? MapSelection.MeasurementName : MapSelection.PinName;
    public string NameLabel => IsMeasurement ? MeasurementNameLabel : PinNameLabel;
    public string RemoveLabel => IsMeasurement ? RemoveMeasurementLabel : RemovePinLabel;

    /// <summary>The name of the picked pin or measurement as it is typed.</summary>
    [ObservableProperty]
    public partial string Name { get; set; } = string.Empty;

    /// <summary>The look of the picked pin.</summary>
    [ObservableProperty]
    public partial PinLookChoice? SelectedPinLook { get; set; }

    /// <summary>The looks a pin can have: plain first, then one with the glyph of each kind of marker.</summary>
    public IReadOnlyList<PinLookChoice> PinLooks => field ??= CreatePinLooks();

    /// <summary>The boxes of blocks that belong to the picked structure, such as its pieces.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<BlockBox> Boxes { get; set; } = [];

    public ObservableCollection<string> Details { get; } = [];

    private readonly MapViewModel _map;
    private readonly IComputeService _computeService;
    private readonly IStructureDescriptionService _descriptionService;
    private readonly IPinService _pinService;
    private readonly IMeasurementService _measurementService;
    private readonly IStructureCatalog _structureCatalog;
    private readonly IMapStyleService _styleService;
    private CancellationTokenSource? _loading;
    private MapPin? _shownPin;

    public SelectionViewModel(MapViewModel map, IComputeService computeService, IStructureDescriptionService descriptionService, IPinService pinService,
        IMeasurementService measurementService, IStructureCatalog structureCatalog, IMapStyleService styleService, INotificationService notificationService) : base(notificationService)
    {
        _map = map;
        _computeService = computeService;
        _descriptionService = descriptionService;
        _pinService = pinService;
        _measurementService = measurementService;
        _structureCatalog = structureCatalog;
        _styleService = styleService;

        map.PropertyChanged += OnMapChanged;
    }

    [RelayCommand]
    public void Close() => _map.Selection = null;

    /// <summary>Takes the picked pin or measurement away and closes the callout.</summary>
    [RelayCommand(CanExecute = nameof(IsNamable))]
    public void Remove()
    {
        switch (_map.Selection)
        {
            case { Pin: { } pin }:
                _pinService.Remove(pin);
                break;
            case { Measurement: { } measurement }:
                _measurementService.Remove(measurement);
                break;
            default:
                return;
        }

        _map.Selection = null;
    }

    partial void OnNameChanged(string value)
    {
        if (_map.Selection?.Pin is { } pin)
            _pinService.Rename(pin, value);
        else if (_map.Selection?.Measurement is { } measurement)
            _measurementService.Rename(measurement, value);
    }

    partial void OnSelectedPinLookChanged(PinLookChoice? value)
    {
        if (value is not null && _map.Selection?.Pin is { } pin)
            _pinService.SetIcon(pin, value.Look.Icon);
    }

    private void OnMapChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MapViewModel.Selection))
            ShowSelection(_map.Selection);
    }

    private void ShowSelection(MapSelection? selection)
    {
        _loading?.Cancel();
        _loading = null;
        Details.Clear();
        Boxes = [];
        IsLoading = false;
        IsOpen = selection is not null;
        IsPin = selection?.Pin is not null;
        IsMeasurement = selection?.Measurement is not null;
        var previousPin = _shownPin;
        _shownPin = selection?.Pin;
        if (selection is null)
            return;

        Title = selection.Name;
        Marker = selection.Marker;
        PositionText = BlockPosText.ToLocation(selection.Position);
        Note = selection.Note ?? string.Empty;
        if (selection.Pin is { } pin)
            ShowPin(pin, keepTypedName: pin == previousPin);
        if (selection.Measurement is { } measurement)
            ShowMeasurement(measurement);
        if (selection.Structure is { } type)
            _ = RunGuardedAsync(() => LoadDetailsAsync(selection, type, _map.World));
    }

    private async Task LoadDetailsAsync(MapSelection selection, StructureType type, WorldKey world)
    {
        var loading = new CancellationTokenSource();
        _loading = loading;
        IsLoading = true;
        try
        {
            var details = await _computeService.RunAsync(new StructureDetailsJob(world, type, selection.Position), _priority, loading.Token);
            if (_loading != loading)
                return;

            PositionText = BlockPosText.ToLocation(selection.Position, details.Height);
            foreach (var line in _descriptionService.Describe(details, world.Version))
                Details.Add(line);
            Boxes = GetBoxes(details);
        }
        catch (OperationCanceledException)
        {
            // Another marker was picked before the details arrived.
        }
        finally
        {
            if (_loading == loading)
            {
                _loading = null;
                IsLoading = false;
            }

            loading.Dispose();
        }
    }

    /// <summary>Shows the name and look of a pin; the name typed so far stays while the same pin is picked anew, as it is while it moves.</summary>
    private void ShowPin(MapPin pin, bool keepTypedName)
    {
        if (!keepTypedName)
            Name = pin.Name ?? string.Empty;
        SelectedPinLook = PinLooks.FirstOrDefault(choice => choice.Look == _styleService.GetPinLook(pin.Icon)) ?? PinLooks[0];
    }

    /// <summary>Shows the name of a measurement, where it runs and how long it is, with how far it goes along each axis.</summary>
    private void ShowMeasurement(MapMeasurement measurement)
    {
        Name = measurement.Name ?? string.Empty;
        PositionText = $"From {BlockPosText.ToBlock(measurement.Start)} to {BlockPosText.ToBlock(measurement.End)}";
        Note = string.Create(CultureInfo.InvariantCulture,
            $"{measurement.Length:0.#} blocks  ·  X {measurement.EndX - measurement.StartX:+0;-0;0}, Z {measurement.EndZ - measurement.StartZ:+0;-0;0}");
    }

    private List<PinLookChoice> CreatePinLooks()
        =>
        [
            new() { Display = PlainPinLabel, Look = _styleService.PlainPin },
            .. _structureCatalog.Structures
                .DistinctBy(structure => structure.Name, StringComparer.Ordinal)
                .Select(structure => new PinLookChoice { Display = structure.Name, Look = _styleService.GetPinLook(MarkerNames.Of(structure.Type)) }),
            new() { Display = SpawnPinLabel, Look = _styleService.GetPinLook(MarkerNames.Spawn) },
            new() { Display = StrongholdPinLabel, Look = _styleService.GetPinLook(MarkerNames.Stronghold) },
        ];

    private static BlockBox[] GetBoxes(StructureDetails details)
        => details.Variant?.Box is { } box
            ? [box, .. details.Pieces.Select(piece => piece.Box)]
            : [.. details.Pieces.Select(piece => piece.Box)];
}

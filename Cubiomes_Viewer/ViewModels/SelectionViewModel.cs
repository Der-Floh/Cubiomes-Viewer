using System.Collections.ObjectModel;
using System.ComponentModel;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Service.ClipboardServ;
using Cubiomes_Viewer.Service.MapStyleServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.StructureDescriptionServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Describes the marker that is picked on the map and offers actions for its position.</summary>
public sealed partial class SelectionViewModel : ViewModelBase
{
    private static readonly JobPriority _priority = new(JobPriorityClass.Interactive);

    public string CenterLabel { get; } = "Center";
    public string CopyCoordinatesLabel { get; } = "Copy coordinates";
    public string CopyTeleportLabel { get; } = "Copy /tp";
    public string CloseLabel { get; } = "Close";
    public string LoadingLabel { get; } = "Reading details...";
    public string CopiedTitle { get; } = "Copied";

    [ObservableProperty]
    public partial bool IsOpen { get; set; }

    [ObservableProperty]
    public partial string Title { get; set; } = string.Empty;

    [ObservableProperty]
    public partial MapMarker? Marker { get; set; }

    [ObservableProperty]
    public partial string PositionText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsLoading { get; set; }

    /// <summary>The boxes of blocks that belong to the picked structure, such as its pieces.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<BlockBox> Boxes { get; set; } = [];

    public ObservableCollection<string> Details { get; } = [];

    private readonly MapViewModel _map;
    private readonly IComputeService _computeService;
    private readonly IStructureDescriptionService _descriptionService;
    private readonly IClipboardService _clipboardService;
    private readonly INotificationService _notificationService;
    private CancellationTokenSource? _loading;

    public SelectionViewModel(MapViewModel map, IComputeService computeService, IStructureDescriptionService descriptionService, IClipboardService clipboardService, INotificationService notificationService) : base(notificationService)
    {
        _map = map;
        _computeService = computeService;
        _descriptionService = descriptionService;
        _clipboardService = clipboardService;
        _notificationService = notificationService;

        map.PropertyChanged += OnMapChanged;
    }

    [RelayCommand]
    public void Center()
    {
        if (_map.Selection is { } selection)
            _map.GoTo(selection.Position.X, selection.Position.Z);
    }

    [RelayCommand]
    public Task CopyCoordinatesAsync() => CopyAsync(BlockPosText.ToCoordinates);

    [RelayCommand]
    public Task CopyTeleportAsync() => CopyAsync(BlockPosText.ToTeleportCommand);

    [RelayCommand]
    public void Close() => _map.Selection = null;

    private Task CopyAsync(Func<BlockPos, string> format) => RunGuardedAsync(async () =>
    {
        if (_map.Selection is not { } selection)
            return;

        var text = format(selection.Position);
        await _clipboardService.SetTextAsync(text);
        _notificationService.ShowSuccess(CopiedTitle, text);
    });

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
        if (selection is null)
            return;

        Title = selection.Name;
        Marker = selection.Marker;
        PositionText = BlockPosText.ToLocation(selection.Position);
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

    private static BlockBox[] GetBoxes(StructureDetails details)
        => details.Variant?.Box is { } box
            ? [box, .. details.Pieces.Select(piece => piece.Box)]
            : [.. details.Pieces.Select(piece => piece.Box)];
}

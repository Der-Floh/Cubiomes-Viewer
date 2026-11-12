using System.Collections.ObjectModel;
using System.Globalization;
using System.Text;

using Avalonia.Platform.Storage;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Analysis;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.Service.NotificationServ;
using Cubiomes_Viewer.Service.StorageServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>
/// One kind of analysis in the Analysis panel: it runs on the scope's seeds and area, lists what it found, opens a
/// chosen line on the map and saves the whole result as a CSV file.
/// </summary>
public abstract partial class AnalysisTabViewModel : ViewModelBase
{
    private static readonly FilePickerFileType _csvFiles = new("CSV files") { Patterns = ["*.csv"], MimeTypes = ["text/csv"] };

    public string StopLabel { get; } = "Stop";
    public string SaveLabel { get; } = "Save as CSV";
    public string OpenHint { get; } = "Choose a line to open its seed there on the map.";

    public abstract string Title { get; }
    public abstract string RunLabel { get; }

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(RunCommand), nameof(StopCommand), nameof(SaveCommand))]
    public partial bool IsRunning { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(SaveCommand))]
    public partial bool HasRows { get; set; }

    [ObservableProperty]
    public partial AnalysisRow? SelectedRow { get; set; }

    public ObservableCollection<AnalysisRow> Rows { get; } = [];

    protected AnalysisScopeViewModel Scope { get; }

    protected abstract string FileName { get; }

    private readonly WorldSession _session;
    private readonly MapViewModel _map;
    private readonly ISeedHistoryService _seedHistory;
    private readonly IStorageService _storage;
    private readonly INotificationService _notifications;
    private CancellationTokenSource? _stopping;
    private CsvTable? _table;

    protected AnalysisTabViewModel(AnalysisScopeViewModel scope, WorldSession session, MapViewModel map, ISeedHistoryService seedHistory, IStorageService storage, INotificationService notifications) : base(notifications)
    {
        Scope = scope;
        _session = session;
        _map = map;
        _seedHistory = seedHistory;
        _storage = storage;
        _notifications = notifications;
    }

    /// <summary>Runs the analysis on the scope as it is now.</summary>
    protected abstract Task<AnalysisResult> AnalyseAsync(IProgress<double> progress, CancellationToken cancellationToken);

    protected static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    protected static string DescribePosition(BlockPos position, BlockPos reference)
        => Format($"at {position.X}, {position.Z} · {position.DistanceTo(reference):N0} blocks away");

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task RunAsync() => RunGuardedAsync(async () =>
    {
        using var stopping = new CancellationTokenSource();
        _stopping = stopping;
        IsRunning = true;
        StatusText = "Working…";
        try
        {
            Show(await AnalyseAsync(new Progress<double>(ShowProgress), stopping.Token));
        }
        catch (OperationCanceledException) when (stopping.IsCancellationRequested)
        {
            StatusText = "Stopped.";
        }
        finally
        {
            _stopping = null;
            IsRunning = false;
        }
    });

    [RelayCommand(CanExecute = nameof(IsRunning))]
    private void Stop() => _stopping?.Cancel();

    [RelayCommand(CanExecute = nameof(CanSave))]
    private Task SaveAsync() => RunGuardedAsync(async () =>
    {
        if (_table is null)
            return;

        var options = new FilePickerSaveOptions { Title = SaveLabel, SuggestedFileName = FileName, DefaultExtension = "csv", FileTypeChoices = [_csvFiles] };
        if (await _storage.SaveFileAsync(options, Encoding.UTF8.GetBytes(_table.ToCsv())) is { } name)
            _notifications.ShowSuccess($"Saved {name}.");
    }, browserFireAndForget: true);

    private bool CanRun() => !IsRunning;

    private bool CanSave() => !IsRunning && HasRows;

    private void ShowProgress(double share)
    {
        if (IsRunning)
            StatusText = Format($"Working… {share * 100:0} %");
    }

    private void Show(AnalysisResult result)
    {
        Rows.Clear();
        foreach (var row in result.Rows)
            Rows.Add(row);
        _table = result.Table;
        HasRows = Rows.Count > 0;
        StatusText = result.Status;
    }

    /// <summary>Shows the seed of the line, in its version, dimension and biome size, at the place of the line.</summary>
    partial void OnSelectedRowChanged(AnalysisRow? value)
    {
        if (value is null)
            return;

        _session.Version = value.World.Version;
        _session.LargeBiomes = value.World.Flags.HasFlag(GeneratorFlags.LargeBiomes);
        _session.Dimension = value.World.Dimension;
        _session.Seed = value.World.Seed;
        _seedHistory.Add(value.World.Seed);
        if (value.Position is { } position)
            _map.GoTo(position.X, position.Z);
    }
}

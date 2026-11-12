using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Service.ClipboardServ;
using Cubiomes_Viewer.Service.NotificationServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Lists the seeds the search found and opens the chosen one on the map.</summary>
public sealed partial class SearchResultsViewModel : ViewModelBase
{
    public string CopyLabel { get; } = "Copy all";
    public string OpenHint { get; } = "Choose a seed to open it on the map.";
    public string NoSeedsLabel { get; } = "No seeds found yet.";

    [ObservableProperty]
    public partial string Title { get; set; } = "Found seeds";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CopyCommand))]
    public partial bool HasSeeds { get; set; }

    [ObservableProperty]
    public partial ComboChoice<long>? SelectedSeed { get; set; }

    /// <summary>The found seeds in the order they were found, which is ascending from the first seed of the search.</summary>
    public ObservableCollection<ComboChoice<long>> Seeds { get; } = [];

    private readonly ISeedSearchService _search;
    private readonly WorldSession _session;
    private readonly ISeedHistoryService _seedHistory;
    private readonly IClipboardService _clipboard;
    private readonly INotificationService _notifications;

    public SearchResultsViewModel(ISeedSearchService search, WorldSession session, ISeedHistoryService seedHistory, IClipboardService clipboard, INotificationService notifications) : base(notifications)
    {
        _search = search;
        _session = session;
        _seedHistory = seedHistory;
        _clipboard = clipboard;
        _notifications = notifications;

        ShowSeeds();
        search.Changed += OnSearchChanged;
    }

    [RelayCommand(CanExecute = nameof(HasSeeds))]
    private Task CopyAsync() => RunGuardedAsync(async () =>
    {
        await _clipboard.SetTextAsync(string.Join(Environment.NewLine, Seeds.Select(seed => seed.Display)));
        _notifications.ShowSuccess($"Copied {SearchProgressText.Count(Seeds.Count, "seed")}.");
    });

    /// <summary>Opens the seed in the version and with the biome size it was found in.</summary>
    partial void OnSelectedSeedChanged(ComboChoice<long>? value)
    {
        if (value is null)
            return;

        if (_search.Setup is { } setup)
        {
            _session.Version = setup.Definition.Version;
            _session.LargeBiomes = setup.Definition.LargeBiomes;
        }

        _session.Seed = value.Value;
        _seedHistory.Add(value.Value);
    }

    private void OnSearchChanged(object? sender, EventArgs e) => ShowSeeds();

    /// <summary>Adds the seeds found since the last change; a new or replaced search starts the list over.</summary>
    private void ShowSeeds()
    {
        var found = _search.Results;
        if (found.Count < Seeds.Count || (Seeds.Count > 0 && found[0] != Seeds[0].Value))
            Seeds.Clear();

        for (var index = Seeds.Count; index < found.Count; index++)
            Seeds.Add(new ComboChoice<long> { Display = found[index].ToString(CultureInfo.InvariantCulture), Value = found[index] });

        HasSeeds = Seeds.Count > 0;
        Title = HasSeeds ? $"Found seeds ({Seeds.Count.ToString("N0", CultureInfo.InvariantCulture)})" : "Found seeds";
    }
}

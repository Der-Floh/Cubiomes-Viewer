using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Service.ConditionServ;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>
/// Edits what a search looks for and which seeds it scans. The search runs in the version and with the biome size
/// the map shows, so that a found seed looks on the map as it was found.
/// </summary>
public sealed partial class SearchSetupViewModel : ObservableObject
{
    public const int AutomaticWorkers = 0;

    public string ConditionsLabel { get; } = "Conditions";
    public string NoConditionsHint { get; } = "Add what a seed has to have. A condition looks around the world origin, or around where another condition was found.";
    public string AddConditionLabel { get; } = "Add a condition";
    public string SeedsLabel { get; } = "Seeds";
    public string MaxResultsLabel { get; } = "Stop after finding";
    public string WorkersLabel { get; } = "Workers";
    public decimal MaxResultsLimit { get; } = SearchSetup.MaxResultsLimit;

    [ObservableProperty]
    public partial decimal? MaxResults { get; set; } = SearchSetup.DefaultMaxResults;

    [ObservableProperty]
    public partial ComboChoice<int>? SelectedWorkers { get; set; }

    [ObservableProperty]
    public partial string VersionText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasConditions { get; set; }

    [ObservableProperty]
    public partial bool HasProblems { get; set; }

    public SeedSourceViewModel Seeds { get; }
    public ObservableCollection<SearchConditionViewModel> Conditions { get; } = [];
    public IReadOnlyList<ConditionKindViewModel> ConditionKinds { get; }
    public ObservableCollection<ComboChoice<int>> WorkerChoices { get; }

    /// <summary>What keeps the search from running that is not about one condition.</summary>
    public ObservableCollection<string> Problems { get; } = [];

    public bool HasWorkerChoices => WorkerChoices.Count > 2;

    /// <summary>The search as it is set up; seeds that cannot be read keep their last readable values.</summary>
    public SearchSetup Setup { get; private set; }

    /// <summary>Whether the search can run as it is set up.</summary>
    public bool IsValid { get; private set; }

    public event EventHandler? Changed;

    private readonly SearchConditionFactory _factory;
    private readonly IConditionValidator _validator;
    private readonly ISeedSequenceFactory _sequenceFactory;
    private readonly IVersionCatalog _versionCatalog;
    private readonly WorldSession _session;
    private bool _isShowing;

    public SearchSetupViewModel(SeedSourceViewModel seeds, SearchConditionFactory factory, IConditionValidator validator, ISeedSequenceFactory sequenceFactory, IVersionCatalog versionCatalog, ISeedSearchService search, WorldSession session)
    {
        Seeds = seeds;
        _factory = factory;
        _validator = validator;
        _sequenceFactory = sequenceFactory;
        _versionCatalog = versionCatalog;
        _session = session;

        ConditionKinds = [.. factory.Kinds.Select(kind => new ConditionKindViewModel(kind, AddCondition))];
        WorkerChoices = CreateWorkerChoices(search);
        SelectedWorkers = WorkerChoices[0];
        Setup = new SearchSetup { Definition = new SearchDefinition { Version = session.Version, LargeBiomes = session.LargeBiomes, Rules = session.Rules } };
        ShowVersion();
        Refresh();
        session.PropertyChanged += OnSessionChanged;
        seeds.Changed += OnSeedsChanged;
    }

    /// <summary>Shows a stored setup, such as the one of the last session.</summary>
    public void Show(SearchSetup setup)
    {
        ArgumentNullException.ThrowIfNull(setup);

        Show(() =>
        {
            Conditions.Clear();
            foreach (var condition in setup.Definition.Conditions)
                Conditions.Add(_factory.Create(condition, _session.Version, OnConditionEdited, OnConditionRemoved));

            Seeds.Show(setup.Source);
            MaxResults = setup.MaxResults;
            SelectedWorkers = WorkerChoices.FirstOrDefault(choice => choice.Value == setup.Workers) ?? WorkerChoices[0];
        });

        Setup = setup;
        ShowReferences();
        Refresh();
    }

    partial void OnMaxResultsChanged(decimal? value) => Refresh();

    partial void OnSelectedWorkersChanged(ComboChoice<int>? value) => Refresh();

    private void AddCondition(ConditionDescriptor kind)
    {
        if (FindFreeId() is not { } id)
            return;

        var condition = _factory.Create(kind.Create(id), _session.Version, OnConditionEdited, OnConditionRemoved);
        condition.IsExpanded = true;
        Conditions.Add(condition);
        ShowReferences();
        Refresh();
    }

    private int? FindFreeId()
    {
        var used = Conditions.Select(condition => condition.Id).ToHashSet();
        for (var id = SearchCondition.MinId; id <= SearchCondition.MaxId; id++)
        {
            if (!used.Contains(id))
                return id;
        }

        return null;
    }

    private void OnSeedsChanged(object? sender, EventArgs e) => Refresh();

    private void OnConditionEdited(SearchConditionViewModel condition)
    {
        ShowReferences();
        Refresh();
    }

    /// <summary>The conditions that looked around the removed one look around what it looked around.</summary>
    private void OnConditionRemoved(SearchConditionViewModel condition)
    {
        foreach (var child in Conditions.Where(other => other.RelativeTo == condition.Id))
            child.LookAround(condition.RelativeTo);

        Conditions.Remove(condition);
        ShowReferences();
        Refresh();
    }

    private void OnSessionChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is not (nameof(WorldSession.Version) or nameof(WorldSession.LargeBiomes) or nameof(WorldSession.Rules)))
            return;

        foreach (var condition in Conditions)
            condition.ShowVersion(_session.Version);
        ShowVersion();
        ShowReferences();
        Refresh();
    }

    /// <summary>Lets each condition look around any other one that does not look around it, directly or not.</summary>
    private void ShowReferences()
    {
        foreach (var condition in Conditions)
        {
            var descendants = GetDescendants(condition.Id);
            condition.ShowReferences([.. Conditions.Where(other => other != condition && !descendants.Contains(other.Id)).Select(other => (other.Id, other.Header))]);
            condition.HasRelativeConditions = Conditions.Any(other => other.RelativeTo == condition.Id);
        }

        HasConditions = Conditions.Count > 0;
    }

    private HashSet<int> GetDescendants(int id)
    {
        HashSet<int> descendants = [];
        Queue<int> pending = new([id]);
        while (pending.TryDequeue(out var parent))
        {
            foreach (var child in Conditions.Where(condition => condition.RelativeTo == parent))
            {
                if (descendants.Add(child.Id))
                    pending.Enqueue(child.Id);
            }
        }

        return descendants;
    }

    private void ShowVersion()
    {
        var name = _versionCatalog.GetName(_session.Version);
        var worlds = _session.LargeBiomes ? $"{name} worlds with large biomes" : $"{name} worlds";
        VersionText = _session.Rules.IsDefault
            ? $"Searches {worlds}, as the map shows them."
            : $"Searches {worlds} with the changed structure rules of the world settings, as the map shows them.";
    }

    /// <summary>Rebuilds the setup from what is entered and shows what keeps it from running.</summary>
    private void Refresh()
    {
        if (_isShowing)
            return;

        List<string> problems = [.. Seeds.Problems];
        if (problems.Count == 0)
            problems.AddRange(_sequenceFactory.Validate(Seeds.Source, _session.Version));

        var definition = new SearchDefinition
        {
            Version = _session.Version,
            LargeBiomes = _session.LargeBiomes,
            Rules = _session.Rules,
            Conditions = [.. Conditions.Select(condition => condition.ToCondition())],
        };
        Setup = new SearchSetup
        {
            Definition = definition,
            Source = Seeds.Source,
            MaxResults = (int)Math.Clamp(Math.Round(MaxResults ?? SearchSetup.DefaultMaxResults), 1, SearchSetup.MaxResultsLimit),
            Workers = SelectedWorkers?.Value ?? AutomaticWorkers,
        };

        IReadOnlyList<ConditionProblem> conditionProblems = Conditions.Count > 0 ? _validator.Validate(definition) : [];
        ShowProblems(conditionProblems, problems);
        IsValid = Conditions.Count > 0 && conditionProblems.Count == 0 && problems.Count == 0;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void ShowProblems(IReadOnlyList<ConditionProblem> conditionProblems, List<string> setupProblems)
    {
        foreach (var condition in Conditions)
            condition.ShowProblems(conditionProblems.Where(problem => problem.ConditionId == condition.Id).Select(problem => problem.Message));

        Problems.Clear();
        foreach (var problem in conditionProblems.Where(problem => Conditions.All(condition => condition.Id != problem.ConditionId)))
            Problems.Add(problem.ToString());
        foreach (var problem in setupProblems)
            Problems.Add(problem);
        HasProblems = Problems.Count > 0;
    }

    private void Show(Action changes)
    {
        var wasShowing = _isShowing;
        _isShowing = true;
        try
        {
            changes();
        }
        finally
        {
            _isShowing = wasShowing;
        }
    }

    private static ObservableCollection<ComboChoice<int>> CreateWorkerChoices(ISeedSearchService search)
    {
        var automatic = string.Create(CultureInfo.InvariantCulture, $"Automatic ({search.AutomaticWorkers})");
        return
        [
            new() { Display = automatic, Value = AutomaticWorkers },
            .. Enumerable.Range(1, search.MaxWorkers).Select(count => new ComboChoice<int> { Display = count.ToString(CultureInfo.InvariantCulture), Value = count }),
        ];
    }
}

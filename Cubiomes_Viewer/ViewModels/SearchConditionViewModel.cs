using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ConditionServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>
/// Edits one condition of a search: where it looks, how many matches it asks for and the settings of its kind.
/// Every edit is reported to the owner, which rebuilds the definition from all conditions.
/// </summary>
public sealed partial class SearchConditionViewModel : ObservableObject
{
    /// <summary>The reference choice of the world origin, which is no condition.</summary>
    public const int OriginReference = 0;

    private const string OriginName = "The world origin";

    public string ReferenceLabel { get; } = "Looks around";
    public string ShapeLabel { get; } = "Area";
    public string DistanceLabel { get; } = "Blocks from the point";
    public string FromLabel { get; } = "From X and Z";
    public string ToLabel { get; } = "To X and Z";
    public string CountLabel { get; } = "At least";
    public string ExcludeLabel { get; } = "None may be there";
    public string AnchorLabel { get; } = "Conditions relative to it start from";
    public string RemoveLabel { get; } = "Remove condition";
    public decimal MinOffset { get; } = -SearchArea.MaxOffset;
    public decimal MaxOffset { get; } = SearchArea.MaxOffset;

    public int Id { get; }
    public ConditionDescriptor Descriptor { get; }
    public IConditionFields? Fields { get; }
    public decimal MaxCount => Descriptor.MaxCount;
    public bool HasArea => Descriptor.HasArea;
    public bool HasCount => Descriptor.MaxCount > 1;
    public bool CanExclude => Descriptor.CanExclude;
    public string Header => string.Create(CultureInfo.InvariantCulture, $"{Id} · {Fields?.Title ?? Descriptor.Name}");

    /// <summary>The condition whose match this one looks around, or <see langword="null"/> for the world origin.</summary>
    public int? RelativeTo { get; private set; }

    [ObservableProperty]
    public partial string Summary { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    [ObservableProperty]
    public partial ComboChoice<int>? SelectedReference { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsDistanceShown), nameof(IsRectangleShown))]
    public partial ComboChoice<AreaShape>? SelectedShape { get; set; }

    [ObservableProperty]
    public partial decimal? Distance { get; set; }

    [ObservableProperty]
    public partial decimal? FromX { get; set; }

    [ObservableProperty]
    public partial decimal? FromZ { get; set; }

    [ObservableProperty]
    public partial decimal? ToX { get; set; }

    [ObservableProperty]
    public partial decimal? ToZ { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsCountShown), nameof(IsAnchorShown))]
    public partial bool IsExcluded { get; set; }

    [ObservableProperty]
    public partial decimal? Count { get; set; }

    /// <summary>Whether other conditions look around this one, which is when its anchor matters.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAnchorShown))]
    public partial bool HasRelativeConditions { get; set; }

    [ObservableProperty]
    public partial ComboChoice<MatchAnchor>? SelectedAnchor { get; set; }

    [ObservableProperty]
    public partial bool HasProblems { get; set; }

    public bool IsDistanceShown => HasArea && SelectedShape?.Value != AreaShape.Rectangle;
    public bool IsRectangleShown => HasArea && SelectedShape?.Value == AreaShape.Rectangle;
    public bool IsCountShown => HasCount && !IsExcluded;
    public bool IsAnchorShown => HasRelativeConditions && HasCount && !IsExcluded;

    /// <summary>The world origin and the conditions this one can look around without looking around itself.</summary>
    public ObservableCollection<ComboChoice<int>> ReferenceChoices { get; } = [];

    public ObservableCollection<ComboChoice<AreaShape>> Shapes { get; } =
    [
        new() { Display = "Square around the point", Value = AreaShape.Square },
        new() { Display = "Circle around the point", Value = AreaShape.Circle },
        new() { Display = "Rectangle", Value = AreaShape.Rectangle },
    ];

    public ObservableCollection<ComboChoice<MatchAnchor>> Anchors { get; } =
    [
        new() { Display = "Any of its matches", Value = MatchAnchor.AnyMatch },
        new() { Display = "Its match nearest to its point", Value = MatchAnchor.Nearest },
        new() { Display = "The middle of its matches", Value = MatchAnchor.Center },
    ];

    public ObservableCollection<string> Problems { get; } = [];

    private readonly Action<SearchConditionViewModel> _edited;
    private readonly Action<SearchConditionViewModel> _removed;
    private bool _isShowing;

    public SearchConditionViewModel(SearchCondition condition, ConditionDescriptor descriptor, Func<Action, IConditionFields?> createFields, Action<SearchConditionViewModel> edited, Action<SearchConditionViewModel> removed)
    {
        ArgumentNullException.ThrowIfNull(condition);
        ArgumentNullException.ThrowIfNull(createFields);

        Id = condition.Id;
        Descriptor = descriptor;
        RelativeTo = condition.RelativeTo;
        _edited = edited;
        _removed = removed;
        Fields = createFields(OnFieldsChanged);
        Show(() =>
        {
            ShowArea(condition.Area);
            IsExcluded = condition.IsExclusion;
            Count = Math.Max(condition.Count, 1);
            SelectedAnchor = Anchors.FirstOrDefault(choice => choice.Value == condition.Anchor) ?? Anchors[0];
            ShowReferences([]);
        });
        Summary = Describe();
    }

    /// <summary>Builds the condition as it is edited.</summary>
    public SearchCondition ToCondition()
    {
        var condition = Descriptor.Create(Id) with
        {
            RelativeTo = RelativeTo,
            Count = IsExcluded ? 0 : HasCount ? Math.Max(1, ToInt(Count)) : 1,
            Anchor = SelectedAnchor?.Value ?? MatchAnchor.AnyMatch,
        };

        if (HasArea)
            condition = condition with { Area = CreateArea() };
        return Fields?.Apply(condition) ?? condition;
    }

    /// <summary>Offers these conditions to look around; the choice stays unless its condition is gone.</summary>
    public void ShowReferences(IReadOnlyList<(int Id, string Header)> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);

        List<(int Id, string Header)> choices = [(OriginReference, OriginName), .. candidates];
        if (!ReferenceChoices.Select(choice => (choice.Value, choice.Display)).SequenceEqual(choices))
        {
            Show(() =>
            {
                ReferenceChoices.Clear();
                foreach (var (id, header) in choices)
                    ReferenceChoices.Add(new ComboChoice<int> { Display = header, Value = id });
            });
        }

        Show(() => SelectedReference = ReferenceChoices.FirstOrDefault(choice => choice.Value == (RelativeTo ?? OriginReference)) ?? ReferenceChoices[0]);
    }

    /// <summary>Moves the condition to look around another one, as when the one it looked around is removed.</summary>
    public void LookAround(int? relativeTo)
    {
        RelativeTo = relativeTo;
        Summary = Describe();
    }

    public void ShowVersion(McVersion version) => Fields?.ShowVersion(version);

    public void ShowProblems(IEnumerable<string> problems)
    {
        Problems.Clear();
        foreach (var problem in problems)
            Problems.Add(problem);
        HasProblems = Problems.Count > 0;
    }

    [RelayCommand]
    private void Remove() => _removed(this);

    partial void OnSelectedReferenceChanged(ComboChoice<int>? value)
    {
        if (_isShowing || value is null)
            return;

        RelativeTo = value.Value == OriginReference ? null : value.Value;
        ReportEdit();
    }

    /// <summary>A rectangle starts out as the square or circle it replaces.</summary>
    partial void OnSelectedShapeChanged(ComboChoice<AreaShape>? value)
    {
        if (!_isShowing && value?.Value == AreaShape.Rectangle)
            Show(() => ShowRectangle(-ToInt(Distance), -ToInt(Distance), ToInt(Distance), ToInt(Distance)));
        ReportEdit();
    }

    partial void OnDistanceChanged(decimal? value) => ReportEdit();

    partial void OnFromXChanged(decimal? value) => ReportEdit();

    partial void OnFromZChanged(decimal? value) => ReportEdit();

    partial void OnToXChanged(decimal? value) => ReportEdit();

    partial void OnToZChanged(decimal? value) => ReportEdit();

    partial void OnIsExcludedChanged(bool value) => ReportEdit();

    partial void OnCountChanged(decimal? value) => ReportEdit();

    partial void OnSelectedAnchorChanged(ComboChoice<MatchAnchor>? value) => ReportEdit();

    private void OnFieldsChanged()
    {
        OnPropertyChanged(nameof(Header));
        ReportEdit();
    }

    private void ReportEdit()
    {
        if (_isShowing)
            return;

        Summary = Describe();
        _edited(this);
    }

    private void ShowArea(SearchArea area)
    {
        var shape = area.Shape;
        SelectedShape = Shapes.First(choice => choice.Value == shape);
        Distance = shape switch
        {
            AreaShape.Circle => area.Radius,
            AreaShape.Square => area.MaxX,
            AreaShape.Rectangle => Math.Max(area.SizeX, area.SizeZ) / 2,
            _ => throw new NotSupportedException($"Unsupported area shape: {shape}"),
        };
        ShowRectangle(area.MinX, area.MinZ, area.MaxX, area.MaxZ);
    }

    private void ShowRectangle(int fromX, int fromZ, int toX, int toZ)
    {
        FromX = fromX;
        FromZ = fromZ;
        ToX = toX;
        ToZ = toZ;
    }

    private SearchArea CreateArea()
    {
        var distance = ToInt(Distance);
        return SelectedShape?.Value switch
        {
            AreaShape.Circle => SearchArea.Circle(distance),
            AreaShape.Rectangle => SearchArea.Rectangle(ToInt(FromX), ToInt(FromZ), ToInt(ToX), ToInt(ToZ)),
            _ => new SearchArea(-distance, -distance, distance, distance),
        };
    }

    /// <summary>Sums the condition up in a line, such as "at least 2 · within 512 blocks of the world origin".</summary>
    private string Describe()
    {
        if (!HasArea)
            return Descriptor.Description;

        string?[] parts = [Fields?.Detail, DescribeCount(), DescribeArea()];
        return string.Join(" · ", parts.Where(part => !string.IsNullOrEmpty(part)));
    }

    private string? DescribeCount()
    {
        if (IsExcluded)
            return "none";
        return HasCount && ToInt(Count) > 1 ? string.Create(CultureInfo.InvariantCulture, $"at least {ToInt(Count)}") : null;
    }

    private string DescribeArea()
    {
        var reference = RelativeTo is { } parent ? string.Create(CultureInfo.InvariantCulture, $"condition {parent}") : "the world origin";
        return SelectedShape?.Value switch
        {
            AreaShape.Circle => $"within a radius of {Format(Distance)} blocks around {reference}",
            AreaShape.Rectangle => $"from {Format(FromX)}, {Format(FromZ)} to {Format(ToX)}, {Format(ToZ)} relative to {reference}",
            _ => $"within {Format(Distance)} blocks of {reference}",
        };
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

    private static string Format(decimal? value) => ToInt(value).ToString("N0", CultureInfo.InvariantCulture);

    private static int ToInt(decimal? value) => (int)Math.Clamp(Math.Round(value ?? 0), int.MinValue, int.MaxValue);
}

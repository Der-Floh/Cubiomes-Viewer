using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.SearchServ;
using Cubiomes_Viewer.Engine.Service.SeedServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Edits where the seeds of a search come from: a range, the seeds with a quad of huts, or a list.</summary>
public sealed partial class SeedSourceViewModel : ObservableObject
{
    public string SourceLabel { get; } = "Seeds from";
    public string FirstSeedLabel { get; } = "From seed";
    public string LastSeedLabel { get; } = "To seed";
    public string LastSeedPlaceholder { get; } = "No end";
    public string QuadTypeLabel { get; } = "Structure";
    public string QuadQualityLabel { get; } = "The huts at least as close as";
    public string QuadRegionLabel { get; } = "First region of the quad, X and Z";
    public string QuadHint { get; } = "Tries every quad-hut base of the class with all 65,536 values of the upper 16 bits, so the four huts attempt to generate in these regions and the region after each. Regions -1, -1 put the quad around the world origin. The bases are found once, which takes a few seconds.";
    public string SeedListPlaceholder { get; } = "One seed per line, as a number or as text";
    public decimal MaxRegion { get; } = SeedSequenceFactory.MaxRegion;
    public decimal MinRegion { get; } = -SeedSequenceFactory.MaxRegion;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsRangeSource), nameof(IsQuadSource), nameof(IsListSource))]
    public partial ComboChoice<SeedSourceKind>? SelectedKind { get; set; }

    [ObservableProperty]
    public partial string FirstSeedText { get; set; } = "0";

    [ObservableProperty]
    public partial string LastSeedText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial ComboChoice<StructureType>? SelectedQuadType { get; set; }

    [ObservableProperty]
    public partial ComboChoice<QuadQuality>? SelectedQuadQuality { get; set; }

    [ObservableProperty]
    public partial decimal? QuadRegionX { get; set; } = QuadHutSeedSource.AroundTheOrigin;

    [ObservableProperty]
    public partial decimal? QuadRegionZ { get; set; } = QuadHutSeedSource.AroundTheOrigin;

    [ObservableProperty]
    public partial string SeedListText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SeedListSummary { get; set; } = string.Empty;

    public ObservableCollection<ComboChoice<SeedSourceKind>> Kinds { get; } =
    [
        new() { Display = "A range of seeds", Value = SeedSourceKind.Range },
        new() { Display = "Seeds with quad huts", Value = SeedSourceKind.QuadHuts },
        new() { Display = "A list of seeds", Value = SeedSourceKind.List },
    ];

    public ObservableCollection<ComboChoice<StructureType>> QuadTypes { get; }
    public IReadOnlyList<ComboChoice<QuadQuality>> QuadQualities => QuadChoices.Qualities;

    public bool IsRangeSource => SelectedKind?.Value == SeedSourceKind.Range;
    public bool IsQuadSource => SelectedKind?.Value == SeedSourceKind.QuadHuts;
    public bool IsListSource => SelectedKind?.Value == SeedSourceKind.List;

    /// <summary>The source as it is entered; seeds that cannot be read keep their last readable value.</summary>
    public SeedSource Source { get; private set; } = new SeedRangeSource();

    /// <summary>What cannot be read of what is entered.</summary>
    public IReadOnlyList<string> Problems { get; private set; } = [];

    public event EventHandler? Changed;

    private readonly ISeedParser _seedParser;
    private bool _isShowing;

    public SeedSourceViewModel(IStructureCatalog structureCatalog, ISeedParser seedParser)
    {
        _seedParser = seedParser;
        QuadTypes = [.. QuadChoices.CreateTypeChoices(structureCatalog)];
        Show(new SeedRangeSource());
    }

    /// <summary>Shows a stored source.</summary>
    public void Show(SeedSource source)
    {
        ArgumentNullException.ThrowIfNull(source);

        _isShowing = true;
        try
        {
            ShowKind(source switch
            {
                QuadHutSeedSource => SeedSourceKind.QuadHuts,
                SeedListSource => SeedSourceKind.List,
                _ => SeedSourceKind.Range,
            });
            ShowRange(source as SeedRangeSource ?? new SeedRangeSource());
            ShowQuad(source as QuadHutSeedSource ?? new QuadHutSeedSource());
            SeedListText = source is SeedListSource list ? string.Join(Environment.NewLine, list.Seeds.Select(FormatSeed)) : string.Empty;
        }
        finally
        {
            _isShowing = false;
        }

        Source = source;
        Refresh();
    }

    partial void OnSelectedKindChanged(ComboChoice<SeedSourceKind>? value) => Refresh();

    partial void OnFirstSeedTextChanged(string value) => Refresh();

    partial void OnLastSeedTextChanged(string value) => Refresh();

    partial void OnSelectedQuadTypeChanged(ComboChoice<StructureType>? value) => Refresh();

    partial void OnSelectedQuadQualityChanged(ComboChoice<QuadQuality>? value) => Refresh();

    partial void OnQuadRegionXChanged(decimal? value) => Refresh();

    partial void OnQuadRegionZChanged(decimal? value) => Refresh();

    partial void OnSeedListTextChanged(string value) => Refresh();

    private void ShowKind(SeedSourceKind kind) => SelectedKind = Kinds.First(choice => choice.Value == kind);

    private void ShowRange(SeedRangeSource range)
    {
        FirstSeedText = FormatSeed(range.FirstSeed);
        LastSeedText = range.LastSeed == long.MaxValue ? string.Empty : FormatSeed(range.LastSeed);
    }

    private void ShowQuad(QuadHutSeedSource quad)
    {
        SelectedQuadType = QuadTypes.FirstOrDefault(choice => choice.Value == quad.Type) ?? QuadTypes[0];
        SelectedQuadQuality = QuadQualities.FirstOrDefault(choice => choice.Value == quad.Quality) ?? QuadQualities[^1];
        QuadRegionX = quad.RegionX;
        QuadRegionZ = quad.RegionZ;
    }

    private void Refresh()
    {
        if (_isShowing)
            return;

        List<string> problems = [];
        Source = SelectedKind?.Value switch
        {
            SeedSourceKind.QuadHuts => CreateQuadSource(),
            SeedSourceKind.List => CreateListSource(problems),
            _ => CreateRangeSource(problems),
        };
        Problems = problems;
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private SeedRangeSource CreateRangeSource(List<string> problems)
    {
        var last = Source as SeedRangeSource ?? new SeedRangeSource();
        var firstSeed = ReadSeed(FirstSeedText, "first", problems) ?? last.FirstSeed;
        var lastSeed = string.IsNullOrWhiteSpace(LastSeedText) ? long.MaxValue : ReadSeed(LastSeedText, "last", problems) ?? last.LastSeed;
        return new SeedRangeSource { FirstSeed = firstSeed, LastSeed = lastSeed };
    }

    private QuadHutSeedSource CreateQuadSource() => new()
    {
        Type = SelectedQuadType?.Value ?? StructureType.SwampHut,
        Quality = SelectedQuadQuality?.Value ?? QuadQuality.Barely,
        RegionX = ToRegion(QuadRegionX),
        RegionZ = ToRegion(QuadRegionZ),
    };

    /// <summary>Reads one seed per line like the world bar does, numbers as they are and text by its hash.</summary>
    private SeedListSource CreateListSource(List<string> problems)
    {
        long[] seeds = [.. SeedListText.Split('\n').Select(line => line.Trim()).Where(line => line.Length > 0).Select(line => _seedParser.Parse(line).Seed)];
        SeedListSummary = seeds.Length == 1 ? "1 seed." : string.Create(CultureInfo.InvariantCulture, $"{seeds.Length:N0} seeds.");
        if (seeds.Length > SeedListSource.MaxSeeds)
            problems.Add(string.Create(CultureInfo.InvariantCulture, $"A list may hold at most {SeedListSource.MaxSeeds:N0} seeds."));
        return new SeedListSource { Seeds = seeds };
    }

    private static long? ReadSeed(string text, string which, List<string> problems)
    {
        if (long.TryParse(text.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var seed))
            return seed;

        problems.Add(string.Create(CultureInfo.InvariantCulture, $"The {which} seed must be a whole number from {long.MinValue} to {long.MaxValue}."));
        return null;
    }

    private static int ToRegion(decimal? value) => (int)Math.Clamp(Math.Round(value ?? QuadHutSeedSource.AroundTheOrigin), int.MinValue, int.MaxValue);

    private static string FormatSeed(long seed) => seed.ToString(CultureInfo.InvariantCulture);
}

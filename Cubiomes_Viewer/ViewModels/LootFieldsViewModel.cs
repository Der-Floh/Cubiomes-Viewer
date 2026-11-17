using System.Collections.ObjectModel;
using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Loot;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.LootServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>
/// The item a structure or stronghold condition asks the chests of its structures to hold. The fields show while chest loot is on
/// in the settings and the chests can be read, and while the condition asks for an item even when not, so that it can be removed.
/// </summary>
public sealed partial class LootFieldsViewModel : ObservableObject
{
    private const string NoChoice = "";

    private static readonly ComboChoice<string> _anything = new() { Display = "Anything", Value = NoChoice };
    private static readonly ComboChoice<string> _anyEnchantment = new() { Display = "Any or none", Value = NoChoice };

    public string ItemLabel { get; } = "Its chests hold";
    public string CountLabel { get; } = "Items, at least";
    public string ScopeLabel { get; } = "Counted";
    public string EnchantmentLabel { get; } = "Enchanted with";
    public string LevelLabel { get; } = "Enchantment level, at least";
    public decimal CountMinimum { get; } = 1;
    public decimal CountMaximum { get; } = LootRequirement.MaxCount;
    public decimal LevelMinimum { get; } = 1;

    public IReadOnlyList<ComboChoice<LootScope>> Scopes { get; } =
    [
        new() { Display = "In one chest", Value = LootScope.OneChest },
        new() { Display = "In all chests together", Value = LootScope.AllChests },
    ];

    [ObservableProperty]
    public partial bool IsShown { get; set; }

    /// <summary>Whether an item is asked for, which is when how many of it and an enchantment can be chosen.</summary>
    [ObservableProperty]
    public partial bool HasItem { get; set; }

    [ObservableProperty]
    public partial bool HasEnchantments { get; set; }

    [ObservableProperty]
    public partial bool HasLevels { get; set; }

    [ObservableProperty]
    public partial decimal LevelMaximum { get; set; } = 1;

    [ObservableProperty]
    public partial ComboChoice<string>? SelectedItem { get; set; }

    [ObservableProperty]
    public partial decimal? MinCount { get; set; } = 1;

    [ObservableProperty]
    public partial ComboChoice<LootScope>? SelectedScope { get; set; }

    [ObservableProperty]
    public partial ComboChoice<string>? SelectedEnchantment { get; set; }

    [ObservableProperty]
    public partial decimal? MinLevel { get; set; } = 1;

    /// <summary>"Anything", then the items the chests can hold, and the one asked for even when they cannot.</summary>
    public ObservableCollection<ComboChoice<string>> Items { get; } = [];

    /// <summary>"Any or none", then the enchantments the chests put on the item, and the one asked for even when they do not.</summary>
    public ObservableCollection<ComboChoice<string>> Enchantments { get; } = [];

    /// <summary>The item the chests have to hold as it is edited; <see langword="null"/> when they may hold anything.</summary>
    public LootRequirement? Requirement { get; private set; }

    public string? Detail => Requirement is { } requirement ? Describe(requirement) : null;

    private readonly ILootCatalog _catalog;
    private readonly Action _changed;
    private StructureType _type;
    private McVersion _version;
    private bool _isChestLootOn;
    private bool _isShowing;

    public LootFieldsViewModel(LootRequirement? requirement, StructureType type, McVersion version, ILootCatalog catalog, Action changed)
    {
        _catalog = catalog;
        _changed = changed;
        _type = type;
        _version = version;
        Requirement = requirement;
        Show(ShowRequirement);
    }

    /// <summary>Shows the fields while chest loot is on in the settings.</summary>
    public void ShowChestLoot(bool isOn)
    {
        _isChestLootOn = isOn;
        ShowVisibility();
    }

    /// <summary>Offers the items of another structure type; an item its chests never hold is no longer asked for.</summary>
    public void ShowType(StructureType type)
    {
        _type = type;
        if (Requirement is { } requirement && _catalog.GetItems(type, _version).All(item => item.Id != requirement.Item))
            Requirement = null;
        Show(ShowRequirement);
    }

    /// <summary>Offers the items of another version; the item asked for stays, and the condition's problems tell when the version lacks it.</summary>
    public void ShowVersion(McVersion version)
    {
        _version = version;
        Show(ShowRequirement);
    }

    /// <summary>
    /// Another item drops the enchantment asked for, as it may not carry it. The items stay as they are, since a combo box does not
    /// show a choice set again while it reports the one picked.
    /// </summary>
    partial void OnSelectedItemChanged(ComboChoice<string>? value)
    {
        if (_isShowing || value is null)
            return;

        Requirement = value.Value == NoChoice
            ? null
            : new LootRequirement { Item = value.Value, MinCount = ToCount(MinCount), Scope = SelectedScope?.Value ?? LootScope.OneChest };
        Show(ShowDetails);
        _changed();
    }

    partial void OnMinCountChanged(decimal? value) => Edit(requirement => requirement with { MinCount = ToCount(value) });

    partial void OnSelectedScopeChanged(ComboChoice<LootScope>? value)
    {
        if (value is not null)
            Edit(requirement => requirement with { Scope = value.Value });
    }

    partial void OnSelectedEnchantmentChanged(ComboChoice<string>? value)
    {
        if (_isShowing || value is null || Requirement is null)
            return;

        var enchantment = value.Value == NoChoice ? null : new LootEnchantment { Id = value.Value, MinLevel = 1 };
        Requirement = Requirement with { Enchantment = enchantment };
        Show(ShowLevels);
        _changed();
    }

    partial void OnMinLevelChanged(decimal? value)
        => Edit(requirement => requirement.Enchantment is { } enchantment ? requirement with { Enchantment = enchantment with { MinLevel = ToLevel(value) } } : requirement);

    private void Edit(Func<LootRequirement, LootRequirement> change)
    {
        if (_isShowing || Requirement is null)
            return;

        Requirement = change(Requirement);
        _changed();
    }

    private void ShowRequirement()
    {
        Items.Clear();
        Items.Add(_anything);
        var items = _catalog.GetItems(_type, _version);
        foreach (var item in items)
            Items.Add(new ComboChoice<string> { Display = item.Name, Value = item.Id });
        if (Requirement is { } requirement && items.All(item => item.Id != requirement.Item))
            Items.Add(new ComboChoice<string> { Display = _catalog.GetItemName(requirement.Item), Value = requirement.Item });

        SelectedItem = Items.FirstOrDefault(choice => choice.Value == (Requirement?.Item ?? NoChoice)) ?? _anything;
        ShowDetails();
    }

    /// <summary>Shows how many of the item are asked for and where, and the enchantments it can carry.</summary>
    private void ShowDetails()
    {
        MinCount = Requirement?.MinCount ?? 1;
        SelectedScope = Scopes.FirstOrDefault(choice => choice.Value == Requirement?.Scope) ?? Scopes[0];
        HasItem = Requirement is not null;
        ShowVisibility();

        Enchantments.Clear();
        Enchantments.Add(_anyEnchantment);
        var options = GetEnchantmentOptions();
        foreach (var option in options)
            Enchantments.Add(new ComboChoice<string> { Display = option.Name, Value = option.Id });

        var chosen = Requirement?.Enchantment;
        if (chosen is not null && options.All(option => option.Id != chosen.Id))
            Enchantments.Add(new ComboChoice<string> { Display = _catalog.GetEnchantmentName(chosen.Id), Value = chosen.Id });

        SelectedEnchantment = Enchantments.FirstOrDefault(choice => choice.Value == (chosen?.Id ?? NoChoice)) ?? _anyEnchantment;
        HasEnchantments = Enchantments.Count > 1;
        ShowLevels();
    }

    private void ShowVisibility() => IsShown = Requirement is not null || (_isChestLootOn && Items.Count > 1);

    /// <summary>Shows the lowest level of the chosen enchantment, up to the highest the chests give the item.</summary>
    private void ShowLevels()
    {
        var chosen = Requirement?.Enchantment;
        LevelMaximum = Math.Max(GetEnchantmentOptions().FirstOrDefault(option => option.Id == chosen?.Id)?.MaxLevel ?? 1, chosen?.MinLevel ?? 1);
        MinLevel = chosen?.MinLevel ?? 1;
        HasLevels = chosen is not null && LevelMaximum > 1;
    }

    private IReadOnlyList<LootEnchantmentOption> GetEnchantmentOptions()
        => Requirement is { } requirement ? _catalog.GetEnchantments(_type, _version, requirement.Item) : [];

    private string Describe(LootRequirement requirement)
    {
        var item = _catalog.GetItemName(requirement.Item);
        var held = requirement.MinCount > 1 ? string.Create(CultureInfo.InvariantCulture, $"holding {requirement.MinCount} × {item}") : $"holding {item}";
        if (requirement.Enchantment is { } enchantment)
            held += enchantment.MinLevel > 1 ? $" with {_catalog.GetEnchantmentName(enchantment.Id, enchantment.MinLevel)} or more" : $" with {_catalog.GetEnchantmentName(enchantment.Id)}";
        if (requirement.MinCount > 1)
            held += requirement.Scope == LootScope.AllChests ? " in all chests together" : " in one chest";
        return held;
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

    private static int ToCount(decimal? value) => (int)Math.Clamp(Math.Round(value ?? 1), 1, LootRequirement.MaxCount);

    private int ToLevel(decimal? value) => (int)Math.Clamp(Math.Round(value ?? 1), 1, Math.Max(LevelMaximum, 1));
}

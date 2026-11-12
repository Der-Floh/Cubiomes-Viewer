using System.Globalization;

using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Engine.Data.Settings;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>One structure type in the world settings: the salt the game places it with and the one entered to replace it.</summary>
public sealed partial class StructureSaltViewModel : ObservableObject
{
    public StructureType Type { get; }
    public string Name { get; }
    public int GameSalt { get; }
    public string GameSaltHint { get; }
    public decimal MinSalt { get; } = int.MinValue;
    public decimal MaxSalt { get; } = int.MaxValue;

    [ObservableProperty]
    public partial bool IsReplacing { get; set; }

    /// <summary>The entered salt; it starts as the game's, and a cleared box means the game's too.</summary>
    [ObservableProperty]
    public partial decimal? Salt { get; set; }

    /// <summary>Whether the row holds something worth keeping: a replacement, or a salt entered and switched off.</summary>
    public bool IsWorthStoring => IsReplacing || EnteredSalt != GameSalt;

    public int EnteredSalt => Salt is { } salt ? (int)Math.Clamp(Math.Round(salt), int.MinValue, int.MaxValue) : GameSalt;

    // Unset while the constructor assigns the stored state, which is not a change to report.
    private readonly Action<StructureSaltViewModel>? _changed;

    public StructureSaltViewModel(StructureType type, string name, int gameSalt, StructureSaltSetting? stored, Action<StructureSaltViewModel> changed)
    {
        Type = type;
        Name = name;
        GameSalt = gameSalt;
        GameSaltHint = string.Create(CultureInfo.InvariantCulture, $"The game uses {gameSalt}.");
        IsReplacing = stored?.IsReplacing ?? false;
        Salt = stored?.Salt ?? gameSalt;
        _changed = changed;
    }

    public StructureSaltSetting ToSetting() => new() { Type = Type, Salt = EnteredSalt, IsReplacing = IsReplacing };

    partial void OnIsReplacingChanged(bool value) => _changed?.Invoke(this);

    partial void OnSaltChanged(decimal? value) => _changed?.Invoke(this);
}

using Avalonia;
using Avalonia.Controls;

namespace Cubiomes_Viewer.Controls;

/// <summary>
/// Shows its content with a small info button beside it that opens a longer explanation underneath, so that a panel
/// stays short while the details are one click away.
/// </summary>
public sealed class InfoHint : ContentControl
{
    public const string DefaultToggleTip = "More about this";

    public static readonly StyledProperty<string?> TextProperty = AvaloniaProperty.Register<InfoHint, string?>(nameof(Text));

    public static readonly StyledProperty<bool> IsOpenProperty = AvaloniaProperty.Register<InfoHint, bool>(nameof(IsOpen));

    public static readonly StyledProperty<string> ToggleTipProperty = AvaloniaProperty.Register<InfoHint, string>(nameof(ToggleTip), DefaultToggleTip);

    /// <summary>The explanation the info button opens.</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Whether the explanation is shown.</summary>
    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public string ToggleTip
    {
        get => GetValue(ToggleTipProperty);
        set => SetValue(ToggleTipProperty, value);
    }
}

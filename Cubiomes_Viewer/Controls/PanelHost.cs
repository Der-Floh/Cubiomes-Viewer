using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;

namespace Cubiomes_Viewer.Controls;

/// <summary>
/// Shows the view of the open panel, built through the app's data templates, and keeps the views of panels opened
/// before hidden instead of dropping them, so that a panel opens again without building all its controls anew.
/// </summary>
public sealed class PanelHost : Panel
{
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<PanelHost, object?>(nameof(Content));

    private readonly Dictionary<object, Control> _views = new(ReferenceEqualityComparer.Instance);
    private Control? _shown;

    /// <summary>The view model of the open panel, or <see langword="null"/> while none is open.</summary>
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ContentProperty)
            Show(change.NewValue);
    }

    private void Show(object? content)
    {
        _shown?.IsVisible = false;
        _shown = content is null ? null : GetView(content);
        _shown?.IsVisible = true;
    }

    private Control GetView(object content)
    {
        if (!_views.TryGetValue(content, out var view))
        {
            view = this.FindDataTemplate(content)?.Build(content) ?? new TextBlock { Text = content.ToString() };
            view.DataContext = content;
            _views.Add(content, view);
            Children.Add(view);
        }

        return view;
    }
}

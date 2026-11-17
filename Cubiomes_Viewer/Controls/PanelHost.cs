using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using Avalonia.Layout;
using Avalonia.Threading;

namespace Cubiomes_Viewer.Controls;

/// <summary>
/// Shows the view of the open panel, built through the app's data templates, and keeps the views of panels opened
/// before hidden instead of dropping them, so that a panel opens again without building all its controls anew. Views
/// can also be prepared ahead while the app is idle, so that even the first opening is quick.
/// </summary>
public sealed class PanelHost : Panel
{
    public static readonly StyledProperty<object?> ContentProperty = AvaloniaProperty.Register<PanelHost, object?>(nameof(Content));

    private readonly Dictionary<object, Control> _views = new(ReferenceEqualityComparer.Instance);
    private readonly Queue<object> _toPrepare = new();
    private Control? _shown;

    /// <summary>The view model of the open panel, or <see langword="null"/> while none is open.</summary>
    public object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    /// <summary>
    /// Builds and measures the views of <paramref name="contents"/> that are not built yet, one each time the app has
    /// nothing else to do, so that no input or frame waits for more than one of them.
    /// </summary>
    public void PrepareWhenIdle(IEnumerable<object> contents)
    {
        ArgumentNullException.ThrowIfNull(contents);

        var isPreparing = _toPrepare.Count > 0;
        foreach (var content in contents)
        {
            if (!_views.ContainsKey(content) && !_toPrepare.Contains(content))
                _toPrepare.Enqueue(content);
        }

        if (!isPreparing)
            PrepareNextWhenIdle();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ContentProperty)
            Show(change.NewValue);
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _toPrepare.Clear();
    }

    private void Show(object? content)
    {
        _shown?.IsVisible = false;
        _shown = content is null ? null : GetView(content);
        _shown?.IsVisible = true;
    }

    private void PrepareNextWhenIdle()
    {
        if (_toPrepare.Count > 0)
            Dispatcher.UIThread.Post(PrepareNext, DispatcherPriority.ApplicationIdle);
    }

    private void PrepareNext()
    {
        if (TopLevel.GetTopLevel(this) is null || !_toPrepare.TryDequeue(out var content))
            return;

        if (!_views.ContainsKey(content))
            Prepare(GetView(content));
        PrepareNextWhenIdle();
    }

    /// <summary>Measures a hidden view, which applies its control templates, the slower half of showing it for the first time.</summary>
    private void Prepare(Control view)
    {
        view.IsVisible = true;
        view.Measure(new Size(GetLayoutWidth(), double.PositiveInfinity));
        view.IsVisible = false;
    }

    /// <summary>
    /// The width the views are shown at: the host's own once it has been laid out, before that the inside of the frame
    /// around it, so that a prepared view is measured at the width it will have.
    /// </summary>
    private double GetLayoutWidth()
    {
        if (Bounds.Width > 0)
            return Bounds.Width;
        if (Parent is not Border frame)
            return double.PositiveInfinity;

        var outer = double.IsNaN(frame.Width) ? (frame.Parent as Layoutable)?.Bounds.Width ?? 0 : frame.Width;
        var inner = outer - frame.BorderThickness.Left - frame.BorderThickness.Right - frame.Padding.Left - frame.Padding.Right;
        return inner > 0 ? inner : double.PositiveInfinity;
    }

    private Control GetView(object content)
    {
        if (!_views.TryGetValue(content, out var view))
        {
            view = this.FindDataTemplate(content)?.Build(content) ?? new TextBlock { Text = content.ToString() };
            view.DataContext = content;
            view.IsVisible = false;
            _views.Add(content, view);
            Children.Add(view);
        }

        return view;
    }
}

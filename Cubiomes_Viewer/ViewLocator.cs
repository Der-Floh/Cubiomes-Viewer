using System.Collections.Frozen;

using Avalonia.Controls;
using Avalonia.Controls.Templates;

using Cubiomes_Viewer.ViewModels;

using Microsoft.Extensions.DependencyInjection;

namespace Cubiomes_Viewer;

/// <summary>
/// Builds the view the composition root paired with a view model through <c>AddView</c>, looked up by the view model's type,
/// so that it works without reflection in a trimmed or ahead-of-time compiled app.
/// </summary>
public sealed class ViewLocator : IDataTemplate
{
    private FrozenDictionary<Type, Func<IServiceProvider, Control>>? _views;

    public Control? Build(object? param)
    {
        if (param is null)
            return null;

        _views ??= App.Services.GetServices<ViewRegistration>().ToFrozenDictionary(view => view.ViewModel, view => view.Create);
        return _views.TryGetValue(param.GetType(), out var create)
            ? create(App.Services)
            : new TextBlock { Text = "Not Found: " + param.GetType().Name };
    }

    public bool Match(object? data) => data is IViewModelBase;
}

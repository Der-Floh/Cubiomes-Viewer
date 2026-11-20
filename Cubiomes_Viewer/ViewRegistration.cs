using Avalonia.Controls;

namespace Cubiomes_Viewer;

/// <summary>Pairs a view model type with the way its view is built, so that the view locator needs no reflection to find the view.</summary>
public sealed record ViewRegistration(Type ViewModel, Func<IServiceProvider, Control> Create);

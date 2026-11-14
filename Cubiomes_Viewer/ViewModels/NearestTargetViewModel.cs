using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Data;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>An entry of the Find nearest menus, with the command that searches for its target.</summary>
public sealed class NearestTargetViewModel
{
    public NearestTarget Target { get; }
    public string Name => Target.Name;
    public IAsyncRelayCommand FindCommand { get; }

    public NearestTargetViewModel(NearestTarget target, Func<NearestTarget, Task> find)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(find);

        Target = target;
        FindCommand = new AsyncRelayCommand(() => find(target), AsyncRelayCommandOptions.AllowConcurrentExecutions);
    }
}

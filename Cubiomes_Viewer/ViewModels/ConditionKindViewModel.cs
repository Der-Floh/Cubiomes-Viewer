using CommunityToolkit.Mvvm.Input;

using Cubiomes_Viewer.Engine.Service.ConditionServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>One kind of condition a search can be given, with the command that adds one.</summary>
public sealed class ConditionKindViewModel
{
    public ConditionDescriptor Descriptor { get; }
    public string Name => Descriptor.Name;
    public string Description => Descriptor.Description;
    public IRelayCommand AddCommand { get; }

    public ConditionKindViewModel(ConditionDescriptor descriptor, Action<ConditionDescriptor> add)
    {
        ArgumentNullException.ThrowIfNull(add);

        Descriptor = descriptor;
        AddCommand = new RelayCommand(() => add(descriptor));
    }
}

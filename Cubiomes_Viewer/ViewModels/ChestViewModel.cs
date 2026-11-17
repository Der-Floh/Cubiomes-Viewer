using CommunityToolkit.Mvvm.ComponentModel;

using Cubiomes_Viewer.Data;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>One container of the picked structure: what and where it is, and what it holds while it is open.</summary>
public sealed partial class ChestViewModel : ObservableObject
{
    public string Title { get; }
    public IReadOnlyList<string> Contents { get; }

    [ObservableProperty]
    public partial bool IsExpanded { get; set; }

    public ChestViewModel(ChestDescription description, bool isExpanded)
    {
        ArgumentNullException.ThrowIfNull(description);

        Title = description.Title;
        Contents = description.Contents;
        IsExpanded = isExpanded;
    }
}

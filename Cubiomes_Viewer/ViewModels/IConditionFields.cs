using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>The settings only one kind of condition has, edited next to the ones every condition has.</summary>
public interface IConditionFields
{
    /// <summary>What the condition is called, such as the structure it looks for.</summary>
    string Title { get; }

    /// <summary>What the condition looks for beyond its title, in a few words, or <see langword="null"/> when the title says it all.</summary>
    string? Detail { get; }

    /// <summary>Carries the edited settings over to a condition of the kind.</summary>
    SearchCondition Apply(SearchCondition condition);

    /// <summary>Offers the choices of another version.</summary>
    void ShowVersion(McVersion version);
}

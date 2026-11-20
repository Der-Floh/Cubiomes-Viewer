namespace Cubiomes_Viewer.Engine.Data.Settings;

/// <summary>Whether the app points out newer releases, where it can tell.</summary>
public sealed class UpdateSettings
{
    /// <summary>Whether the app looks for a newer release when it starts and shows a notice about it.</summary>
    public bool CheckAtStart { get; set; } = true;
}

namespace Cubiomes_Viewer.Engine.Data.Settings;

/// <summary>How the app keeps itself up to date, where it can update itself.</summary>
public sealed class UpdateSettings
{
    /// <summary>Whether the app looks for a newer release when it starts and downloads it.</summary>
    public bool CheckAtStart { get; set; } = true;
}

using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Settings;

/// <summary>How worlds are generated beyond seed and version: which versions are offered and how structures are placed.</summary>
public sealed class WorldSettings
{
    public bool ShowExperimentalVersions { get; set; }
    public bool EstimateTerrain { get; set; } = true;
    public List<StructureSaltSetting> Salts { get; set; } = [];

    public StructureRules ToRules()
    {
        var rules = new StructureRules
        {
            EstimateTerrain = EstimateTerrain,
            Salts = [.. Salts.Where(salt => salt.IsReplacing).Select(salt => new StructureSalt(salt.Type, salt.Salt))],
        };
        return rules.IsDefault ? StructureRules.Default : rules;
    }
}

using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Service.MapStyleServ;

/// <summary>The names the glyphs and colours of markers are keyed by: a structure type's name, or a landmark's.</summary>
public static class MarkerNames
{
    public const string Spawn = "Spawn";
    public const string Stronghold = "Stronghold";

    public static string Of(StructureType type) => type.ToString();

    public static string Of(Dimension dimension) => dimension.ToString();
}

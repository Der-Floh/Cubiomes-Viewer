namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>Something only some structures of a type have, which a structure condition can ask for.</summary>
public enum StructureTrait
{
    /// <summary>Any structure of the type counts.</summary>
    None = 0,

    /// <summary>An end city with an end ship, which carries an elytra.</summary>
    EndShip,

    /// <summary>An abandoned village, full of zombie villagers.</summary>
    Abandoned,

    /// <summary>An igloo with a basement.</summary>
    Basement,

    /// <summary>A giant ruined portal.</summary>
    Giant,
}

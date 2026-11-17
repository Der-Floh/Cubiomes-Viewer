using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

/// <summary>Tells the height a structure lies at.</summary>
public interface IStructureHeightService
{
    /// <summary>
    /// The height of a structure the generator described, or <see langword="null"/> where it cannot be told, as for mineshafts and
    /// ruined portals. Structures that stand on the terrain get an estimate of the surface, where the generator can estimate it.
    /// The generator must be seeded for <paramref name="dimension"/>, the dimension the structure generates in.
    /// </summary>
    StructureHeight? Find(IWorldGenerator generator, Dimension dimension, StructureDetails details);
}

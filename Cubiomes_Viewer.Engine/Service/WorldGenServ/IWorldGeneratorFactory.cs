using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.WorldGenServ;

public interface IWorldGeneratorFactory
{
    /// <summary>Creates a generator whose structures follow <paramref name="rules"/>, or the game's rules when it is <see langword="null"/>.</summary>
    IWorldGenerator Create(McVersion version, GeneratorFlags flags, StructureRules? rules = null);
}

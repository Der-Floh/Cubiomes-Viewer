using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.WorldGenServ;

public interface IWorldGeneratorFactory
{
    IWorldGenerator Create(McVersion version, GeneratorFlags flags);
}

using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.WorldGenServ;

public sealed class CubiomesWorldGeneratorFactory : IWorldGeneratorFactory
{
    public IWorldGenerator Create(McVersion version, GeneratorFlags flags) => new CubiomesWorldGenerator(version, flags);
}

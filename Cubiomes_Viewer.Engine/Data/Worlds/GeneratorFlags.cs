namespace Cubiomes_Viewer.Engine.Data.Worlds;

[Flags]
public enum GeneratorFlags : uint
{
    None = 0,
    LargeBiomes = 1,
    NoBetaOcean = 2,
    ForceOceanVariants = 4,
}

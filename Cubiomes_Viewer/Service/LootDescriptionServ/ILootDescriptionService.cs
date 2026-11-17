using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Loot;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Service.LootDescriptionServ;

public interface ILootDescriptionService
{
    /// <summary>Puts each container of a structure into a title and a line per kind of item, in the order the structure's pieces generate.</summary>
    IReadOnlyList<ChestDescription> Describe(StructureLoot loot);

    /// <summary>What the generator only estimates about the containers of a structure type in a version; <see langword="null"/> where it knows them.</summary>
    string? DescribeAccuracy(StructureType type, McVersion version);
}

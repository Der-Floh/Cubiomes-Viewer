using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Extensions;

public static class StructureTraitExtensions
{
    extension(StructureTrait trait)
    {
        /// <summary>Whether structures of a type can have the trait; ruined portals have theirs in both dimensions.</summary>
        public bool AppliesTo(StructureType type) => trait switch
        {
            StructureTrait.None => true,
            StructureTrait.EndShip => type == StructureType.EndCity,
            StructureTrait.Abandoned => type == StructureType.Village,
            StructureTrait.Basement => type == StructureType.Igloo,
            StructureTrait.Giant => type is StructureType.RuinedPortal or StructureType.RuinedPortalNether,
            _ => false,
        };

        /// <summary>Whether a described structure has the trait.</summary>
        public bool IsIn(StructureDetails details)
        {
            ArgumentNullException.ThrowIfNull(details);
            return trait switch
            {
                StructureTrait.None => true,
                StructureTrait.EndShip => details.Pieces.Any(piece => piece.Type == EndCityPiece.Ship),
                StructureTrait.Abandoned => details.Variant?.IsAbandoned == true,
                StructureTrait.Basement => details.Variant?.HasBasement == true,
                StructureTrait.Giant => details.Variant?.IsGiant == true,
                _ => throw new NotSupportedException($"Unsupported structure trait: {trait}"),
            };
        }
    }
}

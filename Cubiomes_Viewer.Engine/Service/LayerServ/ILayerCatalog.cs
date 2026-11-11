using Cubiomes_Viewer.Engine.Data.Maps;
using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Service.LayerServ;

/// <summary>Knows which layers a map can show for a version and dimension and how each one is named, read and coloured.</summary>
public interface ILayerCatalog
{
    /// <summary>The layers the generator of a version has in a dimension. Biomes come first and are always there.</summary>
    IReadOnlyList<LayerInfo> GetAvailable(McVersion version, Dimension dimension);

    bool IsAvailable(LayerMode mode, McVersion version, Dimension dimension);

    /// <summary>Describes a layer as the generator of a version shows it in a dimension, whether it is available there or not.</summary>
    LayerInfo GetInfo(LayerMode mode, McVersion version, Dimension dimension);
}

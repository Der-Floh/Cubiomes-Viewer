namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>A layer as the generator of one version shows it in one dimension.</summary>
public sealed class LayerInfo
{
    public required LayerMode Mode { get; init; }
    public required string Name { get; init; }

    /// <summary>How the numbers of the layer are read, or <see langword="null"/> for a layer of biomes.</summary>
    public LayerValues? Values { get; init; }

    /// <summary>The most noise octaves the layer can be limited to; zero when it has no octaves to choose from.</summary>
    public int MaxOctaves { get; init; }

    public override string ToString() => Name;
}

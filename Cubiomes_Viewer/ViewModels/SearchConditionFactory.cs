using Cubiomes_Viewer.Data;
using Cubiomes_Viewer.Engine.Data.Search;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Service.ConditionServ;
using Cubiomes_Viewer.Engine.Service.LayerServ;
using Cubiomes_Viewer.Engine.Service.StructureServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;
using Cubiomes_Viewer.Service.MapStyleServ;

namespace Cubiomes_Viewer.ViewModels;

/// <summary>Creates the editors of conditions, each with the fields of its kind.</summary>
public sealed class SearchConditionFactory
{
    /// <summary>The kinds of conditions there are, in the order they are offered.</summary>
    public IReadOnlyList<ConditionDescriptor> Kinds => _registry.Descriptors;

    private readonly IConditionRegistry _registry;
    private readonly IStructureCatalog _structureCatalog;
    private readonly IBiomeCatalog _biomeCatalog;
    private readonly ILayerCatalog _layerCatalog;
    private readonly IMapStyleService _styleService;

    public SearchConditionFactory(IConditionRegistry registry, IStructureCatalog structureCatalog, IBiomeCatalog biomeCatalog, ILayerCatalog layerCatalog, IMapStyleService styleService)
    {
        _registry = registry;
        _structureCatalog = structureCatalog;
        _biomeCatalog = biomeCatalog;
        _layerCatalog = layerCatalog;
        _styleService = styleService;
    }

    public SearchConditionViewModel Create(SearchCondition condition, McVersion version, Action<SearchConditionViewModel> edited, Action<SearchConditionViewModel> removed)
        => new(condition, _registry.GetDescriptor(condition), changed => CreateFields(condition, version, changed), edited, removed);

    private IConditionFields? CreateFields(SearchCondition condition, McVersion version, Action changed) => condition switch
    {
        StructureCondition structure => new StructureFieldsViewModel(structure, version, _structureCatalog, changed),
        QuadHutCondition quad => new QuadHutFieldsViewModel(quad, _structureCatalog, changed),
        BiomeCondition biome => new BiomeFieldsViewModel(biome, version, DimensionChoices.Create(_styleService), _biomeCatalog, changed),
        BiomePatchCondition patch => new BiomePatchFieldsViewModel(patch, version, DimensionChoices.Create(_styleService), _biomeCatalog, changed),
        ClimateCondition climate => new ClimateFieldsViewModel(climate, version, _layerCatalog, changed),
        TemperatureCategoryCondition temperature => new TemperatureCategoryFieldsViewModel(temperature, changed),
        _ => null,
    };
}

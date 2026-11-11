namespace Cubiomes_Viewer.Engine.Data.Search;

/// <summary>Slime chunks lie in the area; a chunk counts when its north-west corner does. A match is the middle of the area.</summary>
public sealed record SlimeChunkCondition : SearchCondition;

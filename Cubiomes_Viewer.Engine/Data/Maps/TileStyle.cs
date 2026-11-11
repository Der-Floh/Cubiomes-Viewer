namespace Cubiomes_Viewer.Engine.Data.Maps;

/// <summary>The choices of the user about how tiles are painted. They do not change what a tile contains.</summary>
public readonly record struct TileStyle(HeightStyle Height = HeightStyle.Tinted);

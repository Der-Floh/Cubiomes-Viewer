namespace Cubiomes_Viewer.Engine.Data.Colors;

/// <summary>The colour a <see cref="ColorRamp"/> has at one position.</summary>
public readonly record struct ColorStop(double Position, RgbColor Color);

using System.Text.Json.Serialization;

using Cubiomes_Viewer.Engine.Data.Worlds;

namespace Cubiomes_Viewer.Engine.Data.Settings;

/// <summary>A distance the user measured on the map of one dimension of a seed and kept: a line from the middle of one block to the middle of another.</summary>
public sealed class MapMeasurement
{
    public long Seed { get; set; }
    public Dimension Dimension { get; set; }
    public int StartX { get; set; }
    public int StartZ { get; set; }
    public int EndX { get; set; }
    public int EndZ { get; set; }

    /// <summary>The name the user gave the measurement, or <see langword="null"/> while it has none.</summary>
    public string? Name { get; set; }

    [JsonIgnore]
    public BlockPos Start => new(StartX, StartZ);

    [JsonIgnore]
    public BlockPos End => new(EndX, EndZ);

    /// <summary>How long the line is, in blocks.</summary>
    [JsonIgnore]
    public double Length => Start.DistanceTo(End);

    public override string ToString() => $"Measurement {Name ?? "unnamed"} from {StartX}, {StartZ} to {EndX}, {EndZ} ({Dimension} of {Seed})";
}

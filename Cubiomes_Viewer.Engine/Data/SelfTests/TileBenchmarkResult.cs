namespace Cubiomes_Viewer.Engine.Data.SelfTests;

public sealed class TileBenchmarkResult
{
    public required int TileCount { get; init; }
    public required int Scale { get; init; }
    public required int WorkerCount { get; init; }
    public required TimeSpan Duration { get; init; }

    public double TilesPerSecond => Duration > TimeSpan.Zero ? TileCount / Duration.TotalSeconds : 0;

    public override string ToString()
    {
        var workers = WorkerCount == 1 ? "worker" : "workers";
        return $"{TileCount} tiles at 1:{Scale} in {Duration.TotalMilliseconds:F0} ms ({TilesPerSecond:F0} tiles/s on {WorkerCount} {workers})";
    }
}

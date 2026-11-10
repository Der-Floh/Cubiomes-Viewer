namespace Cubiomes_Viewer.Engine.Data.SelfTests;

public sealed class SelfTestReport
{
    public required IReadOnlyList<SelfTestCheck> Checks { get; init; }
    public required TimeSpan Duration { get; init; }

    public bool Passed => Checks.All(check => check.Passed);
    public int FailedCount => Checks.Count(check => !check.Passed);

    public override string ToString()
        => $"{Checks.Count - FailedCount}/{Checks.Count} checks passed in {Duration.TotalMilliseconds:F0} ms";
}

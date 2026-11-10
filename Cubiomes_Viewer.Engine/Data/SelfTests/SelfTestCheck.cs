namespace Cubiomes_Viewer.Engine.Data.SelfTests;

public sealed class SelfTestCheck
{
    public required string Name { get; init; }
    public required bool Passed { get; init; }
    public required string Detail { get; init; }

    public override string ToString() => $"{(Passed ? "PASS" : "FAIL")} {Name}: {Detail}";
}

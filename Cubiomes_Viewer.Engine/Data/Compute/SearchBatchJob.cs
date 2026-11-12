using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Data.Compute;

/// <summary>
/// Tests <see cref="Count"/> seeds against a definition: the <see cref="Seeds"/> when they are given, and otherwise
/// <see cref="FirstSeed"/> and every <see cref="Stride"/>-th seed after it, wrapping around at the end of the 64 bits.
/// </summary>
public sealed record SearchBatchJob(SearchDefinition Definition, long FirstSeed, int Count, long Stride = 1, long[]? Seeds = null) : ComputeJob<SearchBatchResult>
{
    public long GetSeed(int index) => Seeds is { } seeds ? seeds[index] : unchecked(FirstSeed + index * Stride);

    public bool Equals(SearchBatchJob? other)
        => other is not null && Definition == other.Definition && FirstSeed == other.FirstSeed && Count == other.Count && Stride == other.Stride
            && (Seeds ?? []).SequenceEqual(other.Seeds ?? []) && (Seeds is null) == (other.Seeds is null);

    public override int GetHashCode() => HashCode.Combine(Definition, FirstSeed, Count, Stride, Seeds?.Length);
}

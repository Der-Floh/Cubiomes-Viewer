using Cubiomes_Viewer.Engine.Data.Search;

namespace Cubiomes_Viewer.Engine.Data.Compute;

/// <summary>Tests <see cref="Count"/> consecutive seeds, starting at <see cref="FirstSeed"/>, against a definition.</summary>
public sealed record SearchBatchJob(SearchDefinition Definition, long FirstSeed, int Count) : ComputeJob<SearchBatchResult>;

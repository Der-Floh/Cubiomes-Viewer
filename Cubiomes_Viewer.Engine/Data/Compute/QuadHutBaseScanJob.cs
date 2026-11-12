using Cubiomes_Viewer.Engine.Data.Structures;

namespace Cubiomes_Viewer.Engine.Data.Compute;

/// <summary>Finds the quad-hut bases with the lower 20 bits <see cref="LowBits"/> among <see cref="Count"/> values of the 28 bits above them, from <see cref="FirstHigh"/> on.</summary>
public sealed record QuadHutBaseScanJob(ulong LowBits, uint FirstHigh, uint Count) : ComputeJob<QuadHutBaseScan>;

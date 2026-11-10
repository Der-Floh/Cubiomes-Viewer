using Cubiomes_Viewer.Engine.Data.Structures;

namespace Cubiomes_Viewer.Engine.Data.Compute;

public sealed record SlimeAreaJob(SlimeAreaRequest Request) : ComputeJob<SlimeAreaData>;

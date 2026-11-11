using Cubiomes_Viewer.Engine.Data.Maps;

namespace Cubiomes_Viewer.Engine.Data.Compute;

public sealed record TileJob(TileRequest Request, int Samples) : ComputeJob<TileData>;

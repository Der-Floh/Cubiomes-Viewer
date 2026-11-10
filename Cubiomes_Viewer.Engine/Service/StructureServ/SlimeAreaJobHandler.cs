using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Structures;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

namespace Cubiomes_Viewer.Engine.Service.StructureServ;

public sealed class SlimeAreaJobHandler : IJobHandler<SlimeAreaJob, SlimeAreaData>
{
    private readonly ISlimeChunkFinder _slimeChunkFinder;

    public SlimeAreaJobHandler(ISlimeChunkFinder slimeChunkFinder)
    {
        _slimeChunkFinder = slimeChunkFinder;
    }

    public SlimeAreaData Handle(SlimeAreaJob job, IWorkerContext context, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        var request = job.Request;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(request.SizeChunks);

        var chunks = new byte[request.SizeChunks * request.SizeChunks];
        _slimeChunkFinder.FindSlimeChunks(request.Seed, request.ChunkX, request.ChunkZ, request.SizeChunks, request.SizeChunks, chunks);
        return new SlimeAreaData { Request = request, Chunks = chunks };
    }
}

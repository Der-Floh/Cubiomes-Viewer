using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.SelfTests;
using Cubiomes_Viewer.Engine.Service.ComputeServ;

namespace Cubiomes_Viewer.Engine.Service.SelfTestServ;

public sealed class SelfTestJobHandler : IJobHandler<SelfTestJob, SelfTestReport>
{
    private readonly ISelfTestService _selfTestService;

    public SelfTestJobHandler(ISelfTestService selfTestService)
    {
        _selfTestService = selfTestService;
    }

    public SelfTestReport Handle(SelfTestJob job, IWorkerContext context, CancellationToken cancellationToken)
        => _selfTestService.Run();
}

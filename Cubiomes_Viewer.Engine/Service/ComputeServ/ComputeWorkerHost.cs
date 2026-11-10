using Cubiomes_Viewer.Engine.Service.WorldGenServ;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

/// <summary>
/// The worker side of a cross-runtime compute service: executes serialized jobs with this runtime's own generators.
/// </summary>
public sealed class ComputeWorkerHost : IDisposable
{
    private readonly JobHandlerRegistry _handlers;
    private readonly WorkerContext _context;

    public ComputeWorkerHost(JobHandlerRegistry handlers, IWorldGeneratorFactory generatorFactory)
    {
        _handlers = handlers;
        _context = new WorkerContext(generatorFactory);
    }

    /// <summary>Executes one request and always returns a response; failures are reported inside it.</summary>
    public byte[] Execute(ReadOnlySpan<byte> request)
    {
        try
        {
            var job = ComputeEnvelope.DeserializeJob(request);
            return ComputeEnvelope.SerializeSuccess(_handlers.Invoke(job, _context, CancellationToken.None));
        }
        catch (Exception ex)
        {
            return ComputeEnvelope.SerializeFailure(ex);
        }
    }

    public void Dispose() => _context.Dispose();
}

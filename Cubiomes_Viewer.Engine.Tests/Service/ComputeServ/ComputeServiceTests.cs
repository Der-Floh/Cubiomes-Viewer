using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

using Microsoft.Extensions.Logging.Abstractions;

namespace Cubiomes_Viewer.Engine.Tests.Service.ComputeServ;

public sealed class ComputeServiceTests
{
    private const int FailingValue = -1;

    private readonly List<int> _executionOrder = [];
    private readonly JobHandlerRegistry _handlers;
    private readonly CubiomesWorldGeneratorFactory _generatorFactory = new();

    public ComputeServiceTests()
    {
        _handlers = new JobHandlerRegistry([JobHandlerRegistration.Create(new EchoHandler(_executionOrder))]);
    }

    [Fact]
    public async Task RunAsync_InlineService_ReturnsHandlerResult()
    {
        using var service = new InlineComputeService(_handlers, _generatorFactory);

        var result = await service.RunAsync(new EchoJob(21), new JobPriority(JobPriorityClass.Visible), TestContext.Current.CancellationToken);

        Assert.Equal(42, result);
    }

    [Fact]
    public async Task RunAsync_InlineService_RunsJobsByPriorityThenSubmissionOrder()
    {
        using var service = new InlineComputeService(_handlers, _generatorFactory);

        Task<int>[] tasks =
        [
            service.RunAsync(new EchoJob(1), new JobPriority(JobPriorityClass.Background), TestContext.Current.CancellationToken),
            service.RunAsync(new EchoJob(2), new JobPriority(JobPriorityClass.Visible, 5), TestContext.Current.CancellationToken),
            service.RunAsync(new EchoJob(3), new JobPriority(JobPriorityClass.Visible, 1), TestContext.Current.CancellationToken),
            service.RunAsync(new EchoJob(4), new JobPriority(JobPriorityClass.Interactive), TestContext.Current.CancellationToken),
            service.RunAsync(new EchoJob(5), new JobPriority(JobPriorityClass.Visible, 1), TestContext.Current.CancellationToken),
        ];
        await Task.WhenAll(tasks);

        Assert.Equal([4, 3, 5, 2, 1], _executionOrder);
    }

    [Fact]
    public async Task RunAsync_CancelledBeforeExecution_NeverInvokesTheHandler()
    {
        using var service = new InlineComputeService(_handlers, _generatorFactory);
        using var cancellation = new CancellationTokenSource();

        var task = service.RunAsync(new EchoJob(7), new JobPriority(JobPriorityClass.Visible), cancellation.Token);
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => task);
        await service.RunAsync(new EchoJob(8), new JobPriority(JobPriorityClass.Visible), TestContext.Current.CancellationToken);
        Assert.Equal([8], _executionOrder);
    }

    [Fact]
    public async Task RunAsync_HandlerThrows_FaultsTheTaskAndKeepsServing()
    {
        using var service = new InlineComputeService(_handlers, _generatorFactory);

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => service.RunAsync(new EchoJob(FailingValue), new JobPriority(JobPriorityClass.Visible), TestContext.Current.CancellationToken));

        Assert.Equal(4, await service.RunAsync(new EchoJob(2), new JobPriority(JobPriorityClass.Visible), TestContext.Current.CancellationToken));
    }

    [Fact]
    public void RunAsync_JobWithoutHandler_ThrowsInvalidOperationException()
    {
        using var service = new InlineComputeService(new JobHandlerRegistry([]), _generatorFactory);

        Assert.Throws<InvalidOperationException>(
            () => { _ = service.RunAsync(new EchoJob(1), new JobPriority(JobPriorityClass.Visible), TestContext.Current.CancellationToken); });
    }

    [Fact]
    public async Task RunAsync_InProcessService_CompletesManyJobsOnWorkerThreads()
    {
        using var service = new InProcessComputeService(_handlers, _generatorFactory, NullLogger<InProcessComputeService>.Instance);

        var results = await Task.WhenAll(Enumerable.Range(0, 200).Select(
            value => service.RunAsync(new EchoJob(value), new JobPriority(JobPriorityClass.Visible, value), TestContext.Current.CancellationToken)));

        Assert.Equal(Enumerable.Range(0, 200).Select(value => value * 2), results);
        Assert.InRange(service.WorkerCount, 1, Environment.ProcessorCount);
    }

    [Fact]
    public async Task RunAsync_AfterDispose_ThrowsObjectDisposedException()
    {
        var service = new InProcessComputeService(_handlers, _generatorFactory, NullLogger<InProcessComputeService>.Instance);
        await service.RunAsync(new EchoJob(1), new JobPriority(JobPriorityClass.Visible), TestContext.Current.CancellationToken);

        service.Dispose();

        Assert.Throws<ObjectDisposedException>(
            () => { _ = service.RunAsync(new EchoJob(1), new JobPriority(JobPriorityClass.Visible), TestContext.Current.CancellationToken); });
    }

    private sealed record EchoJob(int Value) : ComputeJob<int>;

    private sealed class EchoHandler(List<int> executionOrder) : IJobHandler<EchoJob, int>
    {
        public int Handle(EchoJob job, IWorkerContext context, CancellationToken cancellationToken)
        {
            if (job.Value == FailingValue)
                throw new InvalidOperationException("The job was asked to fail.");

            lock (executionOrder)
                executionOrder.Add(job.Value);
            return job.Value * 2;
        }
    }
}

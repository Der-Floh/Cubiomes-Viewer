using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Data.Worlds;
using Cubiomes_Viewer.Engine.Extensions;
using Cubiomes_Viewer.Engine.Service.ComputeServ;
using Cubiomes_Viewer.Engine.Service.WorldGenServ;

using Microsoft.Extensions.Logging.Abstractions;

namespace Cubiomes_Viewer.Engine.Tests.Service.ComputeServ;

public sealed class ComputeServiceTests
{
    private const int FailingValue = -1;

    private readonly List<int> _executionOrder = [];
    private readonly GateHandler _gate = new();
    private readonly JobHandlerRegistry _handlers;
    private readonly CubiomesWorldGeneratorFactory _generatorFactory = new();

    public ComputeServiceTests()
    {
        _handlers = new JobHandlerRegistry([JobHandlerRegistration.Create(new EchoHandler(_executionOrder)), JobHandlerRegistration.Create(_gate)]);
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
        var gate = service.RunAsync(new GateJob(), new JobPriority(JobPriorityClass.Interactive), TestContext.Current.CancellationToken);
        Assert.True(_gate.Entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        Task<int>[] tasks =
        [
            service.RunAsync(new EchoJob(1), new JobPriority(JobPriorityClass.Background), TestContext.Current.CancellationToken),
            service.RunAsync(new EchoJob(2), new JobPriority(JobPriorityClass.Visible, 5), TestContext.Current.CancellationToken),
            service.RunAsync(new EchoJob(3), new JobPriority(JobPriorityClass.Visible, 1), TestContext.Current.CancellationToken),
            service.RunAsync(new EchoJob(4), new JobPriority(JobPriorityClass.Interactive), TestContext.Current.CancellationToken),
            service.RunAsync(new EchoJob(5), new JobPriority(JobPriorityClass.Visible, 1), TestContext.Current.CancellationToken),
        ];
        _gate.Released.Set();
        await gate;
        await Task.WhenAll(tasks);

        Assert.Equal([4, 3, 5, 2, 1], _executionOrder);
    }

    [Fact]
    public async Task RunAsync_CancelledBeforeExecution_NeverInvokesTheHandler()
    {
        using var service = new InlineComputeService(_handlers, _generatorFactory);
        using var cancellation = new CancellationTokenSource();
        var gate = service.RunAsync(new GateJob(), new JobPriority(JobPriorityClass.Interactive), TestContext.Current.CancellationToken);
        Assert.True(_gate.Entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));

        var task = service.RunAsync(new EchoJob(7), new JobPriority(JobPriorityClass.Visible), cancellation.Token);
        cancellation.Cancel();
        _gate.Released.Set();
        await gate;

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
    public async Task InlineService_DisposedWhileAJobRuns_LetsItFinishOnItsGeneratorAndFailsTheQueuedOnes()
    {
        var held = new HeldGeneratorHandler();
        var service = new InlineComputeService(new JobHandlerRegistry([JobHandlerRegistration.Create(new EchoHandler(_executionOrder)), JobHandlerRegistration.Create(held)]), _generatorFactory);
        var running = service.RunAsync(new HeldGeneratorJob(new WorldKey(262, McVersion.V1_21_3, Dimension.Overworld, GeneratorFlags.None)), new JobPriority(JobPriorityClass.Interactive), TestContext.Current.CancellationToken);
        Assert.True(held.Entered.Wait(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken));
        var queued = service.RunAsync(new EchoJob(1), new JobPriority(JobPriorityClass.Visible), TestContext.Current.CancellationToken);

        service.Dispose();
        held.Released.Set();

        Assert.InRange(await running, 0, byte.MaxValue);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => queued);
        Assert.Empty(_executionOrder);
        Assert.Throws<ObjectDisposedException>(
            () => { _ = service.RunAsync(new EchoJob(2), new JobPriority(JobPriorityClass.Visible), TestContext.Current.CancellationToken); });
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

    [Fact]
    public async Task InProcessService_DisposedWithJobsStillQueued_FailsThem()
    {
        var gate = new GateHandler();
        var service = new InProcessComputeService(new JobHandlerRegistry([JobHandlerRegistration.Create(new EchoHandler(_executionOrder)), JobHandlerRegistration.Create(gate)]), _generatorFactory, NullLogger<InProcessComputeService>.Instance);
        var holding = Enumerable.Range(0, service.WorkerCount).Select(_ => service.RunAsync(new GateJob(), new JobPriority(JobPriorityClass.Interactive), TestContext.Current.CancellationToken)).ToList();
        Assert.True(SpinWait.SpinUntil(() => gate.EnteredCount == service.WorkerCount, TimeSpan.FromSeconds(10)));
        var queued = service.RunAsync(new EchoJob(1), new JobPriority(JobPriorityClass.Visible), TestContext.Current.CancellationToken);

        var disposing = Task.Run(service.Dispose, TestContext.Current.CancellationToken);
        await Assert.ThrowsAsync<ObjectDisposedException>(() => queued);
        gate.Released.Set();
        await disposing;
        await Task.WhenAll(holding);

        Assert.Empty(_executionOrder);
    }

    [Fact]
    public async Task RunAllAsync_OneJobFails_CancelsTheOthersStillRunning()
    {
        var compute = new ManualCompute();

        var run = compute.RunAllAsync<int>([new EchoJob(1), new EchoJob(2)], new JobPriority(JobPriorityClass.Visible), maxRunning: 2, cancellationToken: TestContext.Current.CancellationToken);
        compute.Fail(0, new InvalidOperationException("The job broke."));

        await Assert.ThrowsAsync<InvalidOperationException>(() => run);
        Assert.True(compute.Tokens[1].IsCancellationRequested);
    }

    private sealed record EchoJob(int Value) : ComputeJob<int>;

    /// <summary>Hands out jobs that end only when the test ends them, keeping the token each one got.</summary>
    private sealed class ManualCompute : IComputeService
    {
        private readonly List<Action<Exception>> _failures = [];

        public int WorkerCount => 2;
        public List<CancellationToken> Tokens { get; } = [];

        public Task<TResult> RunAsync<TResult>(ComputeJob<TResult> job, JobPriority priority, CancellationToken cancellationToken = default)
        {
            var completion = new TaskCompletionSource<TResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            Tokens.Add(cancellationToken);
            _failures.Add(exception => completion.TrySetException(exception));
            return completion.Task;
        }

        public void Fail(int index, Exception exception) => _failures[index](exception);
    }

    /// <summary>A job that holds the worker until it is released, so that jobs queued meanwhile wait together.</summary>
    private sealed record GateJob : ComputeJob<bool>;

    private sealed class GateHandler : IJobHandler<GateJob, bool>
    {
        private int _enteredCount;

        public ManualResetEventSlim Entered { get; } = new();
        public ManualResetEventSlim Released { get; } = new();
        public int EnteredCount => Volatile.Read(ref _enteredCount);

        public bool Handle(GateJob job, IWorkerContext context, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _enteredCount);
            Entered.Set();
            Released.Wait(cancellationToken);
            return true;
        }
    }

    private sealed record HeldGeneratorJob(WorldKey World) : ComputeJob<int>;

    /// <summary>Takes its generator, holds the worker until it is released, and only then asks the generator for a biome.</summary>
    private sealed class HeldGeneratorHandler : IJobHandler<HeldGeneratorJob, int>
    {
        public ManualResetEventSlim Entered { get; } = new();
        public ManualResetEventSlim Released { get; } = new();

        public int Handle(HeldGeneratorJob job, IWorkerContext context, CancellationToken cancellationToken)
        {
            var generator = context.GetGenerator(job.World);
            Entered.Set();
            Released.Wait(cancellationToken);
            return generator.GetBiomeAt(4, 0, 16, 0);
        }
    }

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

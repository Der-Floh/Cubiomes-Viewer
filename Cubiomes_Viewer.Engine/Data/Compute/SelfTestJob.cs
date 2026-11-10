using Cubiomes_Viewer.Engine.Data.SelfTests;

namespace Cubiomes_Viewer.Engine.Data.Compute;

/// <summary>Runs the native generator self-test on the worker that picks the job up.</summary>
public sealed record SelfTestJob : ComputeJob<SelfTestReport>;

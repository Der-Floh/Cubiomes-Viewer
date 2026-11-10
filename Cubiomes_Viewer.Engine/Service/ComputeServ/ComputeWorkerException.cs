namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

/// <summary>A worker in another runtime could not be started or stopped answering.</summary>
public sealed class ComputeWorkerException : Exception
{
    public ComputeWorkerException(string message) : base(message)
    {
    }

    public ComputeWorkerException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

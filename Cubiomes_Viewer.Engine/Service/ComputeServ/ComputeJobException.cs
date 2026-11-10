namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

/// <summary>A job failed on a worker in another runtime, or that worker failed while running it.</summary>
public sealed class ComputeJobException : Exception
{
    public ComputeJobException(string message) : base(message)
    {
    }

    public ComputeJobException(string message, Exception innerException) : base(message, innerException)
    {
    }
}

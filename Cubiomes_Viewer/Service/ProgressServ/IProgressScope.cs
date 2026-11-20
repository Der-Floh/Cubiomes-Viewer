namespace Cubiomes_Viewer.Service.ProgressServ;

/// <summary>One running action's part in the progress the app shows: it reports how far the action has come, from 0 to 1, and disposing it ends that part.</summary>
public interface IProgressScope : IProgress<double>, IDisposable
{
}

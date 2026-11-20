namespace Cubiomes_Viewer.Service.UpdateServ;

/// <summary>The releases of the app cannot be reached or read, for example while offline.</summary>
public sealed class UpdateException(string message, Exception innerException) : Exception(message, innerException);

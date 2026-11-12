namespace Cubiomes_Viewer.Cli.Service.ArgumentServ;

/// <summary>The command line asks for something the CLI does not offer, or leaves out what it needs.</summary>
public sealed class InvalidArgumentsException(string message) : Exception(message);

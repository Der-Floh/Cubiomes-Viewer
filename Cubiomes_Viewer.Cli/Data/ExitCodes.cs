namespace Cubiomes_Viewer.Cli.Data;

/// <summary>What the process returns, for scripts that run it.</summary>
public static class ExitCodes
{
    public const int Success = 0;
    public const int Failure = 1;
    public const int InvalidArguments = 2;

    /// <summary>The search was stopped before it finished and its progress saved: 128 plus SIGINT, as shells report an interrupted command.</summary>
    public const int Stopped = 130;
}

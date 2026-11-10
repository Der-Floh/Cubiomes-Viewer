namespace Cubiomes_Viewer.Engine.Interop;

public sealed class CubiomesNativeException : Exception
{
    public NativeStatus Status { get; }

    public CubiomesNativeException(NativeStatus status, string operation)
        : this($"{operation} failed: {Describe(status)}", status)
    {
    }

    private CubiomesNativeException(string message, NativeStatus status) : base(message)
    {
        Status = status;
    }

    /// <summary>Recreates a failure that was reported by a worker in another runtime.</summary>
    public static CubiomesNativeException FromWorker(NativeStatus status, string message) => new(message, status);

    internal static int ThrowIfError(int result, string operation)
        => result < 0 ? throw new CubiomesNativeException((NativeStatus)result, operation) : result;

    private static string Describe(NativeStatus status) => status switch
    {
        NativeStatus.InvalidArgument => "the native generator rejected an argument (unsupported scale, size, version or dimension).",
        NativeStatus.Unsupported => "the requested feature is not supported for this Minecraft version.",
        NativeStatus.InsufficientCapacity => "the destination buffer is too small.",
        NativeStatus.OutOfMemory => "the native generator ran out of memory.",
        NativeStatus.GenerationFailed => "the native generator could not generate the requested area.",
        NativeStatus.InvalidState => "a seed must be applied before generating.",
        _ => $"unexpected native status {(int)status}.",
    };
}

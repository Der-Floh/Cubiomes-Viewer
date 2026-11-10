namespace Cubiomes_Viewer.Engine.Interop;

public enum NativeStatus
{
    Ok = 0,
    InvalidArgument = -1,
    Unsupported = -2,
    InsufficientCapacity = -3,
    OutOfMemory = -4,
    GenerationFailed = -5,
    InvalidState = -6,
}

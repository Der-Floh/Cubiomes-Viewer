using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

using Cubiomes_Viewer.Engine.Data.Compute;
using Cubiomes_Viewer.Engine.Interop;

namespace Cubiomes_Viewer.Engine.Service.ComputeServ;

/// <summary>
/// The wire format between a caller and a worker in another runtime. A request is the job as JSON;
/// a response is one status byte followed by the result or the error as JSON.
/// </summary>
public static class ComputeEnvelope
{
    private const byte SuccessStatus = 0;
    private const byte FailureStatus = 1;

    public static byte[] SerializeJob(ComputeJob job)
    {
        ArgumentNullException.ThrowIfNull(job);
        return JsonSerializer.SerializeToUtf8Bytes(job, ComputeJsonContext.Default.ComputeJob);
    }

    public static ComputeJob DeserializeJob(ReadOnlySpan<byte> request)
        => JsonSerializer.Deserialize(request, ComputeJsonContext.Default.ComputeJob)
            ?? throw new ArgumentException("The request does not contain a job.", nameof(request));

    public static byte[] SerializeSuccess(object result)
    {
        ArgumentNullException.ThrowIfNull(result);
        return Frame(SuccessStatus, JsonSerializer.SerializeToUtf8Bytes(result, GetTypeInfo(result.GetType())));
    }

    public static byte[] SerializeFailure(Exception exception)
    {
        ArgumentNullException.ThrowIfNull(exception);

        var error = new WorkerError
        {
            Message = exception.Message,
            NativeStatus = exception is CubiomesNativeException native ? (int)native.Status : null,
        };
        return Frame(FailureStatus, JsonSerializer.SerializeToUtf8Bytes(error, ComputeJsonContext.Default.WorkerError));
    }

    /// <summary>Returns the job result, or throws the failure the worker reported.</summary>
    public static TResult DeserializeResponse<TResult>(ReadOnlySpan<byte> response)
    {
        if (response.IsEmpty)
            throw new ArgumentException("The worker returned an empty response.", nameof(response));

        var body = response[1..];
        if (response[0] == SuccessStatus)
        {
            return JsonSerializer.Deserialize(body, (JsonTypeInfo<TResult>)GetTypeInfo(typeof(TResult)))
                ?? throw new ComputeJobException($"The worker returned no '{typeof(TResult).Name}' result.");
        }

        var error = JsonSerializer.Deserialize(body, ComputeJsonContext.Default.WorkerError)
            ?? throw new ComputeJobException("The worker reported a failure without details.");
        throw error.NativeStatus is { } status
            ? CubiomesNativeException.FromWorker((NativeStatus)status, error.Message)
            : new ComputeJobException(error.Message);
    }

    private static JsonTypeInfo GetTypeInfo(Type type)
        => ComputeJsonContext.Default.GetTypeInfo(type)
            ?? throw new NotSupportedException($"'{type.Name}' is not registered in {nameof(ComputeJsonContext)} and cannot cross to a worker.");

    private static byte[] Frame(byte status, byte[] body)
    {
        var framed = new byte[body.Length + 1];
        framed[0] = status;
        body.CopyTo(framed, 1);
        return framed;
    }
}

namespace Cubiomes_Viewer.Engine.Service.ExportServ;

/// <summary>
/// Collects what synchronous encoders write until it is passed on with <see cref="DrainToAsync"/>, so that they can feed
/// destinations that take only asynchronous writes, as files in the browser do.
/// </summary>
internal sealed class DeferredWriteStream : Stream
{
    private readonly MemoryStream _pending = new();
    private long _written;

    public override bool CanRead => false;
    public override bool CanSeek => false;
    public override bool CanWrite => true;
    public override long Length => throw new NotSupportedException();

    public override long Position
    {
        get => _written;
        set => throw new NotSupportedException();
    }

    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        _pending.Write(buffer);
        _written += buffer.Length;
    }

    public async Task DrainToAsync(Stream destination, CancellationToken cancellationToken)
    {
        if (_pending.Length == 0)
            return;

        await destination.WriteAsync(_pending.GetBuffer().AsMemory(0, (int)_pending.Length), cancellationToken);
        _pending.SetLength(0);
    }

    public override void Flush() { }

    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
    public override void SetLength(long value) => throw new NotSupportedException();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
            _pending.Dispose();
        base.Dispose(disposing);
    }
}

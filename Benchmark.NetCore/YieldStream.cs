using System.Runtime.CompilerServices;

namespace Benchmark.NetCore;

/// <summary>
/// A stream that does nothing except yielding the task/thread to take up nonzero time. Modeled after NullStream.
/// </summary>
internal sealed class YieldStream : Stream
{
    public static readonly YieldStream Default = new();

    private YieldStream() { }

    public override bool CanRead => true;
    public override bool CanWrite => true;
    public override bool CanSeek => true;
    public override long Length => 0;
    public override long Position { get => 0; set { } }

    public override void CopyTo(Stream destination, int bufferSize)
    {
        Thread.Yield();
    }

    public override async Task CopyToAsync(Stream destination, int bufferSize, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
    }

    protected override void Dispose(bool disposing)
    {
        // Do nothing - we don't want this stream to be closable.
    }

    public override void Flush()
    {
        Thread.Yield();
    }

    public override async Task FlushAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
    }


    public override int Read(byte[] buffer, int offset, int count)
    {
        Thread.Yield();
        return 0;
    }

    public override int Read(Span<byte> buffer)
    {
        Thread.Yield();
        return 0;
    }

    public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        return 0;
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder<>))]
    public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
        return 0;
    }

    public override int ReadByte()
    {
        Thread.Yield();
        return -1;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        Thread.Yield();
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        Thread.Yield();
    }

    public override async Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
    }

    [AsyncMethodBuilder(typeof(PoolingAsyncValueTaskMethodBuilder))]
    public override async ValueTask WriteAsync(ReadOnlyMemory<byte> buffer, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        await Task.Yield();
    }

    public override void WriteByte(byte value)
    {
        Thread.Yield();
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        Thread.Yield();
        return 0;
    }

    public override void SetLength(long length)
    {
        Thread.Yield();
    }
}

using System;
using System.Buffers;
using System.IO;
using System.Threading;

namespace IMV.Common;

public class ArrayPoolStream : Stream
{
    public byte[] Buffer => _buffer;
    private byte[] _buffer;

    public override bool CanRead => true;

    public override bool CanSeek => true;

    public override bool CanWrite => true;

    public override long Length => Buffer != null ? _length : 0;
    private int _length;

    public override long Position
    {
        get => _position;
        set => _position = value;
    }
    private long _position;

    private ArrayPool<byte> _pool;

    public ArrayPoolStream(Span<byte> buffer) : this(ArrayPool<byte>.Shared, buffer.Length)
    {
        buffer.CopyTo(Buffer);
    }

    public ArrayPoolStream(int size) : this(ArrayPool<byte>.Shared, size)
    {
    }

    public ArrayPoolStream(ArrayPool<byte> pool, int size)
    {
        _pool = pool;
        _buffer = pool.Rent(size);
        _length = size;
    }

    public override void Flush() { }

    public override int Read(Span<byte> buffer)
    {
        if (Buffer == null)
            return 0;

        var readSize = (int)Math.Min(_length - _position, buffer.Length);
        Buffer.AsSpan((int)_position, readSize).CopyTo(buffer);
        _position += readSize;
        return readSize;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        if (Buffer == null)
            return 0;

        if (buffer.Length < offset + count)
            throw new ArgumentException("The size of the buffer is smaller than the offset + count.");

        var readSize = (int)Math.Min(_length - _position, count);
        Buffer.AsSpan((int)_position, readSize).CopyTo(buffer.AsSpan(offset));
        _position += readSize;
        return readSize;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        if (Buffer == null)
            return 0;

        long newPos;
        switch (origin)
        {
            case SeekOrigin.Current:
                newPos = _position + offset;
                break;

            case SeekOrigin.End:
                newPos = _length + offset;
                break;

            default:
                newPos = offset;
                break;
        }

        if (newPos < 0)
        {
            newPos = 0;
        }
        else if (newPos > _length)
        {
            newPos = _length;
        }

        _position = newPos;
        return newPos;
    }

    public override void SetLength(long value)
    {
        throw new NotImplementedException();
    }

    public override void Write(ReadOnlySpan<byte> buffer)
    {
        var writeSize = (int)Math.Min(_length - _position, buffer.Length);
        buffer.Slice(0, writeSize).CopyTo(Buffer.AsSpan((int)_position));
        _position += writeSize;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        if (Buffer == null)
            return;

        if (buffer.Length < offset + count)
            throw new ArgumentException("The size of the buffer is smaller than the offset + count.");

        var writeSize = (int)Math.Min(_length - _position, count);
        buffer.AsSpan(offset, writeSize).CopyTo(Buffer.AsSpan((int)_position));
        _position += writeSize;
    }

    protected override void Dispose(bool disposing)
    {
        var buf = Interlocked.Exchange(ref _buffer, Array.Empty<byte>());
        if (buf.Length > 0)
            _pool.Return(buf);
    }

    public Span<byte> CreateSpanFromPosition() => Buffer.AsSpan(0, (int)_position);
    public Span<byte> CreateSpanFromSize() => Buffer.AsSpan(0, _length);
}

using System.Collections.Generic;
using System.IO;

namespace RandomizerCore.Core;

/// Captures every byte written into a sparse map (offset, value) instead of
/// allocating a full ROM-sized buffer. Reads of unwritten regions return 0.
public sealed class SparseRomBuffer : Stream
{
    private readonly Dictionary<long, byte> _writes = new();
    private long _position;
    private readonly long _length;

    public SparseRomBuffer(long length = 0x2000000)
    {
        _length = length;
    }

    public IReadOnlyDictionary<long, byte> Writes => _writes;

    public override bool CanRead => true;
    public override bool CanSeek => true;
    public override bool CanWrite => true;
    public override long Length => _length;

    public override long Position
    {
        get => _position;
        set => _position = value;
    }

    public override int Read(byte[] buffer, int offset, int count)
    {
        long remaining = _length - _position;
        if (remaining <= 0) return 0;
        int actual = (int)System.Math.Min(count, remaining);
        for (int i = 0; i < actual; i++)
            buffer[offset + i] = _writes.TryGetValue(_position + i, out var b) ? b : (byte)0;
        _position += actual;
        return actual;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i++)
            _writes[_position + i] = buffer[offset + i];
        _position += count;
    }

    public override long Seek(long offset, SeekOrigin origin)
    {
        _position = origin switch
        {
            SeekOrigin.Begin => offset,
            SeekOrigin.Current => _position + offset,
            SeekOrigin.End => _length + offset,
            _ => _position,
        };
        return _position;
    }

    public override void SetLength(long value)
    {
        // Length is fixed by the constructor; no-op.
    }

    public override void Flush() { }
}

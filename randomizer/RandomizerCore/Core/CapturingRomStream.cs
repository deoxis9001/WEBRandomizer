using System.Collections.Generic;
using System.IO;

namespace RandomizerCore.Core;

/// A Stream that captures every Write into a sparse <c>(offset, value)</c> map and
/// returns a constant <c>fill</c> byte for any Read of an offset that has never
/// been written.
///
/// Used by the zero-ROM web flow: the shuffler runs against this stream instead
/// of a real 16/32 MB ROM buffer, so the host never needs the user's ROM.
///
/// The stream is also designed to handle ColorzCore's WriteRom behaviour, which
/// reads the entire output stream into its internal buffer at startup and
/// writes the entire buffer back at the end. Without help, this would blow up
/// the manifest to ~32 M entries. The fix here is the <c>fill</c> trick:
/// any Write whose value equals <c>fill</c> is dropped on the floor (it is
/// indistinguishable from a future Read that returns the fill anyway). To
/// disambiguate "intentional fill-byte writes" from "passthrough", run the
/// pipeline twice with two different fills (e.g. 0x00 and 0xFF) and merge
/// the results: a write of value V is only filtered out in the run whose
/// fill equals V, so it always shows up in the other run.
public sealed class CapturingRomStream : Stream
{
    private readonly Dictionary<long, byte> _writes = new();
    private readonly byte _fill;
    private long _position;
    private readonly long _length;

    public CapturingRomStream(byte fill, long length = 0x2000000)
    {
        _fill = fill;
        _length = length;
    }

    public byte Fill => _fill;

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
            buffer[offset + i] = _writes.TryGetValue(_position + i, out var b) ? b : _fill;
        _position += actual;
        return actual;
    }

    public override void Write(byte[] buffer, int offset, int count)
    {
        for (int i = 0; i < count; i++)
        {
            long off = _position + i;
            byte v = buffer[offset + i];
            if (v == _fill) _writes.Remove(off);
            else _writes[off] = v;
        }
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

    public override void SetLength(long value) { /* fixed */ }
    public override void Flush() { }
}

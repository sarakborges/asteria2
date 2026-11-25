using System.Buffers.Binary;

namespace Asteria.Serialization.Nbt;

/// <summary>
/// Big-endian output primitives and Java modified-UTF encoding for uncompressed NBT streams.
/// </summary>
public sealed class NbtDataOutput : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private bool _disposed;

    public NbtDataOutput(Stream stream, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        if (!stream.CanWrite)
            throw new ArgumentException("The NBT output stream must be writable.", nameof(stream));

        _leaveOpen = leaveOpen;
    }

    public void WriteByte(byte value)
    {
        EnsureNotDisposed();
        _stream.WriteByte(value);
    }

    public void WriteSByte(sbyte value) =>
        WriteByte(unchecked((byte)value));

    public void WriteShort(short value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        BinaryPrimitives.WriteInt16BigEndian(buffer, value);
        Write(buffer);
    }

    public void WriteUnsignedShort(ushort value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        BinaryPrimitives.WriteUInt16BigEndian(buffer, value);
        Write(buffer);
    }

    public void WriteInt(int value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        BinaryPrimitives.WriteInt32BigEndian(buffer, value);
        Write(buffer);
    }

    public void WriteLong(long value)
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        BinaryPrimitives.WriteInt64BigEndian(buffer, value);
        Write(buffer);
    }

    public void WriteFloat(float value) =>
        WriteInt(BitConverter.SingleToInt32Bits(value));

    public void WriteDouble(double value) =>
        WriteLong(BitConverter.DoubleToInt64Bits(value));

    public void Write(ReadOnlySpan<byte> bytes)
    {
        EnsureNotDisposed();
        _stream.Write(bytes);
    }

    /// <summary>
    /// Writes one Java DataOutput.writeUTF value using modified UTF-8 over UTF-16 code units.
    /// NUL and surrogate code units use the two-/three-byte forms defined by the Java format.
    /// </summary>
    public void WriteUTF(string value)
    {
        ArgumentNullException.ThrowIfNull(value);
        EnsureNotDisposed();

        int byteLength = 0;
        foreach (char character in value)
        {
            byteLength = checked(byteLength + GetModifiedUtfLength(character));
            if (byteLength > ushort.MaxValue)
                throw new IOException(
                    $"Modified UTF-8 representation is too long: {byteLength} bytes.");
        }

        WriteUnsignedShort((ushort)byteLength);

        byte[] buffer = new byte[byteLength];
        int index = 0;
        foreach (char character in value)
        {
            int codeUnit = character;
            if (codeUnit is >= 0x0001 and <= 0x007F)
            {
                buffer[index++] = (byte)codeUnit;
            }
            else if (codeUnit <= 0x07FF)
            {
                buffer[index++] = (byte)(0xC0 | ((codeUnit >> 6) & 0x1F));
                buffer[index++] = (byte)(0x80 | (codeUnit & 0x3F));
            }
            else
            {
                buffer[index++] = (byte)(0xE0 | ((codeUnit >> 12) & 0x0F));
                buffer[index++] = (byte)(0x80 | ((codeUnit >> 6) & 0x3F));
                buffer[index++] = (byte)(0x80 | (codeUnit & 0x3F));
            }
        }

        Write(buffer);
    }

    private static int GetModifiedUtfLength(char character) =>
        character is >= '\u0001' and <= '\u007F'
            ? 1
            : character <= '\u07FF'
                ? 2
                : 3;

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    public void Flush()
    {
        EnsureNotDisposed();
        _stream.Flush();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (!_leaveOpen)
            _stream.Dispose();
    }
}
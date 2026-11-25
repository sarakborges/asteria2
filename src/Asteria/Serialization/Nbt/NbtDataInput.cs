using System.Buffers.Binary;
using System.Text;

namespace Asteria.Serialization.Nbt;

/// <summary>
/// Big-endian input primitives and Java modified-UTF decoding for uncompressed NBT streams.
/// </summary>
public sealed class NbtDataInput : IDisposable
{
    private readonly Stream _stream;
    private readonly bool _leaveOpen;
    private bool _disposed;

    public NbtDataInput(Stream stream, bool leaveOpen = false)
    {
        _stream = stream ?? throw new ArgumentNullException(nameof(stream));
        if (!stream.CanRead)
            throw new ArgumentException("The NBT input stream must be readable.", nameof(stream));

        _leaveOpen = leaveOpen;
    }

    public byte ReadByte()
    {
        EnsureNotDisposed();
        int value = _stream.ReadByte();
        return value < 0
            ? throw new EndOfStreamException()
            : (byte)value;
    }

    public sbyte ReadSByte() =>
        unchecked((sbyte)ReadByte());

    public short ReadShort()
    {
        Span<byte> buffer = stackalloc byte[sizeof(short)];
        ReadFully(buffer);
        return BinaryPrimitives.ReadInt16BigEndian(buffer);
    }

    public ushort ReadUnsignedShort()
    {
        Span<byte> buffer = stackalloc byte[sizeof(ushort)];
        ReadFully(buffer);
        return BinaryPrimitives.ReadUInt16BigEndian(buffer);
    }

    public int ReadInt()
    {
        Span<byte> buffer = stackalloc byte[sizeof(int)];
        ReadFully(buffer);
        return BinaryPrimitives.ReadInt32BigEndian(buffer);
    }

    public long ReadLong()
    {
        Span<byte> buffer = stackalloc byte[sizeof(long)];
        ReadFully(buffer);
        return BinaryPrimitives.ReadInt64BigEndian(buffer);
    }

    public float ReadFloat() =>
        BitConverter.Int32BitsToSingle(ReadInt());

    public double ReadDouble() =>
        BitConverter.Int64BitsToDouble(ReadLong());

    public void ReadFully(Span<byte> destination)
    {
        EnsureNotDisposed();
        _stream.ReadExactly(destination);
    }

    /// <summary>
    /// Reads one Java DataInput.readUTF value: an unsigned-short byte length followed
    /// by modified UTF-8 encoded UTF-16 code units.
    /// </summary>
    public string ReadUTF()
    {
        int byteLength = ReadUnsignedShort();
        if (byteLength == 0)
            return string.Empty;

        byte[] bytes = new byte[byteLength];
        ReadFully(bytes);
        char[] characters = new char[byteLength];
        int byteIndex = 0;
        int charIndex = 0;

        while (byteIndex < byteLength)
        {
            int first = bytes[byteIndex] & 0xFF;
            int prefix = first >> 4;

            if (prefix <= 7)
            {
                characters[charIndex++] = (char)first;
                byteIndex++;
                continue;
            }

            if (prefix is 12 or 13)
            {
                if (byteIndex + 1 >= byteLength)
                    throw MalformedModifiedUtf(byteIndex);

                int second = bytes[byteIndex + 1] & 0xFF;
                if ((second & 0xC0) != 0x80)
                    throw MalformedModifiedUtf(byteIndex + 1);

                characters[charIndex++] =
                    (char)(((first & 0x1F) << 6) | (second & 0x3F));
                byteIndex += 2;
                continue;
            }

            if (prefix == 14)
            {
                if (byteIndex + 2 >= byteLength)
                    throw MalformedModifiedUtf(byteIndex);

                int second = bytes[byteIndex + 1] & 0xFF;
                int third = bytes[byteIndex + 2] & 0xFF;
                if ((second & 0xC0) != 0x80)
                    throw MalformedModifiedUtf(byteIndex + 1);
                if ((third & 0xC0) != 0x80)
                    throw MalformedModifiedUtf(byteIndex + 2);

                characters[charIndex++] =
                    (char)(((first & 0x0F) << 12) |
                           ((second & 0x3F) << 6) |
                           (third & 0x3F));
                byteIndex += 3;
                continue;
            }

            throw MalformedModifiedUtf(byteIndex);
        }

        return new string(characters, 0, charIndex);
    }

    /// <summary>
    /// Skips one Java modified-UTF value without allocating or validating its character data.
    /// This is used by NBT container skip paths, which must advance over names without charging
    /// the allocation budget for objects that are not materialized.
    /// </summary>
    public void SkipUTF()
    {
        int byteLength = ReadUnsignedShort();
        if (SkipBytes(byteLength) != byteLength)
            throw new EndOfStreamException();
    }

    /// <summary>Skips up to count bytes, returning the number actually skipped.</summary>
    public int SkipBytes(int count)
    {
        EnsureNotDisposed();
        if (count < 0)
            throw new ArgumentOutOfRangeException(nameof(count));

        byte[] buffer = new byte[Math.Min(count, 512)];
        int skipped = 0;
        while (skipped < count)
        {
            int read = _stream.Read(buffer, 0, Math.Min(buffer.Length, count - skipped));
            if (read == 0)
                break;
            skipped += read;
        }

        return skipped;
    }

    private static IOException MalformedModifiedUtf(int byteIndex) =>
        new($"Malformed modified UTF-8 input at byte {byteIndex}.");

    private void EnsureNotDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
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
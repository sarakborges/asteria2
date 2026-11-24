using System.Globalization;
using System.Text;

namespace Asteria.Content.Chunks;

/// <summary>
/// Stores the 4096 packed light values of one 16x16x16 section.
/// This follows Minecraft Java 26.3's DataLayer representation.
/// </summary>
public sealed class DataLayer
{
    public const int LayerCount = 16;
    public const int LayerSize = 128;
    public const int Size = 2048;

    private byte[]? _data;
    private int _defaultValue;

    public DataLayer()
        : this(0)
    {
    }

    public DataLayer(int defaultValue)
    {
        _defaultValue = defaultValue;
    }

    public DataLayer(byte[] data)
    {
        ArgumentNullException.ThrowIfNull(data);
        _data = data;
        _defaultValue = 0;

        if (data.Length != Size)
        {
            throw new ArgumentException(
                $"DataLayer should be 2048 bytes not: {data.Length}",
                nameof(data));
        }
    }

    public int Get(int x, int y, int z) =>
        Get(GetIndex(x, y, z));

    public void Set(int x, int y, int z, int value) =>
        Set(GetIndex(x, y, z), value);

    private static int GetIndex(int x, int y, int z) =>
        unchecked(y << 8 | z << 4 | x);

    private int Get(int index)
    {
        if (_data is null)
            return _defaultValue;

        int position = GetByteIndex(index);
        int nibble = GetNibbleIndex(index);
        return _data[position] >> (4 * nibble) & 15;
    }

    private void Set(int index, int value)
    {
        byte[] data = GetData();
        int position = GetByteIndex(index);
        int nibble = GetNibbleIndex(index);
        int shift = 4 * nibble;
        int mask = ~(15 << shift);
        int valueToSet = (value & 15) << shift;
        data[position] = (byte)(data[position] & mask | valueToSet);
    }

    private static int GetNibbleIndex(int index) =>
        index & 1;

    private static int GetByteIndex(int index) =>
        index >> 1;

    public void Fill(int value)
    {
        _defaultValue = value;
        _data = null;
    }

    private static byte PackFilled(int value)
    {
        byte packed = unchecked((byte)value);
        for (int shift = 4; shift < 8; shift += 4)
            packed = unchecked((byte)(packed | value << shift));

        return packed;
    }

    public byte[] GetData()
    {
        if (_data is null)
        {
            _data = new byte[Size];
            if (_defaultValue != 0)
                Array.Fill(_data, PackFilled(_defaultValue));
        }

        return _data;
    }

    public DataLayer Copy() =>
        _data is null
            ? new DataLayer(_defaultValue)
            : new DataLayer((byte[])_data.Clone());

    public override string ToString()
    {
        var builder = new StringBuilder();
        for (int index = 0; index < 4096; index++)
        {
            builder.Append(Get(index).ToString("x", CultureInfo.InvariantCulture));
            if ((index & 15) == 15)
                builder.Append('\n');

            if ((index & 0xFF) == 255)
                builder.Append('\n');
        }

        return builder.ToString();
    }

    public string LayerToString(int layer)
    {
        _ = layer;
        var builder = new StringBuilder();
        for (int index = 0; index < 256; index++)
        {
            builder.Append(Get(index).ToString("x", CultureInfo.InvariantCulture));
            if ((index & 15) == 15)
                builder.Append('\n');
        }

        return builder.ToString();
    }

    public bool IsDefinitelyHomogenous() =>
        _data is null;

    public bool IsDefinitelyFilledWith(int value) =>
        _data is null && _defaultValue == value;

    public bool IsEmpty() =>
        _data is null && _defaultValue == 0;
}

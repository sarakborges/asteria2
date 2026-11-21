using System.Collections;

namespace Asteria.Core.Shapes;

/// <summary>
/// Read-only double coordinate sequence used by voxel-shape grids.
/// </summary>
public abstract class DoubleList : IReadOnlyList<double>
{
    public abstract int Count { get; }
    public abstract double GetDouble(int index);
    public double this[int index] => GetDouble(index);

    public IEnumerator<double> GetEnumerator()
    {
        for (int index = 0; index < Count; index++)
            yield return GetDouble(index);
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    public override bool Equals(object? obj)
    {
        if (ReferenceEquals(this, obj))
            return true;
        if (obj is not DoubleList other || Count != other.Count)
            return false;

        for (int index = 0; index < Count; index++)
        {
            if (JavaDoubleCompare(GetDouble(index), other.GetDouble(index)) != 0)
                return false;
        }

        return true;
    }

    public override int GetHashCode()
    {
        var hash = new HashCode();
        for (int index = 0; index < Count; index++)
            hash.Add(JavaDoubleBits(GetDouble(index)));
        return hash.ToHashCode();
    }

    private static int JavaDoubleCompare(double left, double right)
    {
        if (left < right) return -1;
        if (left > right) return 1;
        long leftBits = JavaDoubleBits(left);
        long rightBits = JavaDoubleBits(right);
        return leftBits == rightBits ? 0 : leftBits < rightBits ? -1 : 1;
    }

    private static long JavaDoubleBits(double value) =>
        double.IsNaN(value)
            ? unchecked((long)0x7ff8000000000000UL)
            : BitConverter.DoubleToInt64Bits(value);
}

public sealed class ArrayDoubleList : DoubleList
{
    private readonly double[] _values;

    public ArrayDoubleList(IEnumerable<double> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = values.ToArray();
    }

    public ArrayDoubleList(params double[] values)
    {
        ArgumentNullException.ThrowIfNull(values);
        _values = (double[])values.Clone();
    }

    public override int Count => _values.Length;

    public override double GetDouble(int index) => _values[index];

    public double[] ToArray() => (double[])_values.Clone();
}

public sealed class OffsetDoubleList : DoubleList
{
    private readonly DoubleList _source;
    private readonly double _offset;

    public OffsetDoubleList(DoubleList source, double offset)
    {
        _source = source ?? throw new ArgumentNullException(nameof(source));
        _offset = offset;
    }

    public override int Count => _source.Count;

    public override double GetDouble(int index) => _source.GetDouble(index) + _offset;
}

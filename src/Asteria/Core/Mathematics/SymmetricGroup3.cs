using Asteria.Core;

namespace Asteria.Core.Mathematics;

/// <summary>
/// The six permutations of three axes, with the reference Cayley and inverse tables.
/// </summary>
public sealed class SymmetricGroup3
{
    public static readonly SymmetricGroup3 P123 = new(0, 0, 1, 2);
    public static readonly SymmetricGroup3 P213 = new(1, 1, 0, 2);
    public static readonly SymmetricGroup3 P132 = new(2, 0, 2, 1);
    public static readonly SymmetricGroup3 P312 = new(3, 2, 0, 1);
    public static readonly SymmetricGroup3 P231 = new(4, 1, 2, 0);
    public static readonly SymmetricGroup3 P321 = new(5, 2, 1, 0);

    private static readonly SymmetricGroup3[] Values =
    {
        P123, P213, P132, P312, P231, P321
    };

    private static readonly SymmetricGroup3[][] CayleyTable = CreateCayleyTable();
    private static readonly SymmetricGroup3[] InverseTable = CreateInverseTable();

    private readonly int _ordinal;
    internal int Ordinal => _ordinal;
    private readonly int _p0;
    private readonly int _p1;
    private readonly int _p2;

    public Matrix3x3 Transformation { get; }

    private SymmetricGroup3(int ordinal, int p0, int p1, int p2)
    {
        _ordinal = ordinal;
        _p0 = p0;
        _p1 = p1;
        _p2 = p2;
        Transformation = Matrix3x3.CreatePermutation(p0, p1, p2);
    }

    public static SymmetricGroup3[] GetValues() => (SymmetricGroup3[])Values.Clone();

    public SymmetricGroup3 Compose(SymmetricGroup3 that)
    {
        ArgumentNullException.ThrowIfNull(that);
        return CayleyTable[_ordinal][that._ordinal];
    }

    public SymmetricGroup3 Inverse() => InverseTable[_ordinal];

    public int Permute(int index) =>
        index switch
        {
            0 => _p0,
            1 => _p1,
            2 => _p2,
            _ => throw new ArgumentException("Must be 0, 1 or 2, but got " + index, nameof(index))
        };

    public Direction.Axis PermuteAxis(Direction.Axis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        Direction.Axis[] axes = Direction.Axis.GetValues();
        return axes[Permute(AxisIndex(axis))];
    }

    public MutableVec3f PermuteVector(MutableVec3f vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        float v0 = vector.Get(_p0);
        float v1 = vector.Get(_p1);
        float v2 = vector.Get(_p2);
        return vector.Set(v0, v1, v2);
    }

    public MutableVec3i PermuteVector(MutableVec3i vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        int v0 = vector.Get(_p0);
        int v1 = vector.Get(_p1);
        int v2 = vector.Get(_p2);
        return vector.Set(v0, v1, v2);
    }

    public override string ToString() =>
        _ordinal switch
        {
            0 => nameof(P123),
            1 => nameof(P213),
            2 => nameof(P132),
            3 => nameof(P312),
            4 => nameof(P231),
            _ => nameof(P321)
        };

    private static SymmetricGroup3[][] CreateCayleyTable()
    {
        var table = new SymmetricGroup3[Values.Length][];
        for (int firstIndex = 0; firstIndex < Values.Length; firstIndex++)
        {
            SymmetricGroup3 first = Values[firstIndex];
            table[firstIndex] = new SymmetricGroup3[Values.Length];

            for (int secondIndex = 0; secondIndex < Values.Length; secondIndex++)
            {
                SymmetricGroup3 second = Values[secondIndex];
                int p0 = first.Permute(second._p0);
                int p1 = first.Permute(second._p1);
                int p2 = first.Permute(second._p2);
                table[firstIndex][secondIndex] =
                    Values.First(value => value._p0 == p0 && value._p1 == p1 && value._p2 == p2);
            }
        }

        return table;
    }

    private static SymmetricGroup3[] CreateInverseTable()
    {
        var result = new SymmetricGroup3[Values.Length];
        foreach (SymmetricGroup3 value in Values)
        {
            result[value._ordinal] =
                Values.First(candidate => ReferenceEquals(value.Compose(candidate), P123));
        }

        return result;
    }

    private static int AxisIndex(Direction.Axis axis)
    {
        if (ReferenceEquals(axis, Direction.Axis.X))
            return 0;
        if (ReferenceEquals(axis, Direction.Axis.Y))
            return 1;
        if (ReferenceEquals(axis, Direction.Axis.Z))
            return 2;

        throw new ArgumentException("Unknown direction axis.", nameof(axis));
    }
}

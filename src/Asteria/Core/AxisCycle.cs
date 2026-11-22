namespace Asteria.Core;

/// <summary>
/// Cyclic permutation of the X/Y/Z coordinate axes, matching the 26.3 AxisCycle values.
/// </summary>
public sealed class AxisCycle
{
    public static readonly AxisCycle NONE = new("none", 0);
    public static readonly AxisCycle FORWARD = new("forward", 1);
    public static readonly AxisCycle BACKWARD = new("backward", 2);

    private static readonly AxisCycle[] VALUES = { NONE, FORWARD, BACKWARD };
    private static readonly Direction.Axis[] AXIS_VALUES = Direction.Axis.GetValues();

    public string Name { get; }
    private readonly int _cycle;

    private AxisCycle(string name, int cycle)
    {
        Name = name;
        _cycle = cycle;
    }

    public static AxisCycle[] GetValues() => (AxisCycle[])VALUES.Clone();

    public int Cycle(int x, int y, int z, Direction.Axis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        return _cycle switch
        {
            0 => axis.Choose(x, y, z),
            1 => axis.Choose(z, x, y),
            2 => axis.Choose(y, z, x),
            _ => throw new InvalidOperationException()
        };
    }

    public double Cycle(double x, double y, double z, Direction.Axis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        return _cycle switch
        {
            0 => axis.Choose(x, y, z),
            1 => axis.Choose(z, x, y),
            2 => axis.Choose(y, z, x),
            _ => throw new InvalidOperationException()
        };
    }

    public Direction.Axis Cycle(Direction.Axis axis)
    {
        int index = GetAxisIndex(axis);
        return AXIS_VALUES[PositiveModulo(index + _cycle, AXIS_VALUES.Length)];
    }

    public AxisCycle Inverse() =>
        _cycle switch
        {
            0 => NONE,
            1 => BACKWARD,
            2 => FORWARD,
            _ => throw new InvalidOperationException()
        };

    public static AxisCycle Between(Direction.Axis from, Direction.Axis to)
    {
        int difference = GetAxisIndex(to) - GetAxisIndex(from);
        return VALUES[PositiveModulo(difference, VALUES.Length)];
    }

    private static int GetAxisIndex(Direction.Axis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        if (ReferenceEquals(axis, Direction.Axis.X)) return 0;
        if (ReferenceEquals(axis, Direction.Axis.Y)) return 1;
        if (ReferenceEquals(axis, Direction.Axis.Z)) return 2;
        throw new ArgumentException("Unknown direction axis.", nameof(axis));
    }

    private static int PositiveModulo(int value, int modulus) =>
        (value % modulus + modulus) % modulus;

    public override string ToString() => Name;
}

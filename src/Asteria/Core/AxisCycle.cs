namespace Asteria.Core;

/// <summary>
/// Cyclic permutation of the three spatial axes.
/// </summary>
public enum AxisCycle
{
    None,
    Forward,
    Backward
}

public static class AxisCycleExtensions
{
    public static Direction.Axis Cycle(this AxisCycle cycle, Direction.Axis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        return cycle switch
        {
            AxisCycle.None => axis,
            AxisCycle.Forward => axis switch
            {
                var value when ReferenceEquals(value, Direction.Axis.X) => Direction.Axis.Y,
                var value when ReferenceEquals(value, Direction.Axis.Y) => Direction.Axis.Z,
                _ => Direction.Axis.X
            },
            AxisCycle.Backward => axis switch
            {
                var value when ReferenceEquals(value, Direction.Axis.X) => Direction.Axis.Z,
                var value when ReferenceEquals(value, Direction.Axis.Y) => Direction.Axis.X,
                _ => Direction.Axis.Y
            },
            _ => throw new ArgumentOutOfRangeException(nameof(cycle))
        };
    }

    public static AxisCycle Inverse(this AxisCycle cycle) =>
        cycle switch
        {
            AxisCycle.None => AxisCycle.None,
            AxisCycle.Forward => AxisCycle.Backward,
            AxisCycle.Backward => AxisCycle.Forward,
            _ => throw new ArgumentOutOfRangeException(nameof(cycle))
        };

    public static (T X, T Y, T Z) Cycle<T>(this AxisCycle cycle, T x, T y, T z) =>
        cycle switch
        {
            AxisCycle.None => (x, y, z),
            AxisCycle.Forward => (y, z, x),
            AxisCycle.Backward => (z, x, y),
            _ => throw new ArgumentOutOfRangeException(nameof(cycle))
        };

    public static AxisCycle Between(Direction.Axis first, Direction.Axis second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        if (ReferenceEquals(first, second))
            return AxisCycle.None;
        if (ReferenceEquals(first, Direction.Axis.X) && ReferenceEquals(second, Direction.Axis.Y) ||
            ReferenceEquals(first, Direction.Axis.Y) && ReferenceEquals(second, Direction.Axis.Z) ||
            ReferenceEquals(first, Direction.Axis.Z) && ReferenceEquals(second, Direction.Axis.X))
            return AxisCycle.Forward;

        return AxisCycle.Backward;
    }
}

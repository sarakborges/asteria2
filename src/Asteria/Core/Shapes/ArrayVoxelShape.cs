using Asteria.Core;

namespace Asteria.Core.Shapes;

/// <summary>
/// Voxel shape with explicit coordinate boundaries for each discrete axis.
/// </summary>
public sealed class ArrayVoxelShape : VoxelShape
{
    private readonly DoubleList _xs;
    private readonly DoubleList _ys;
    private readonly DoubleList _zs;

    public ArrayVoxelShape(
        DiscreteVoxelShape shape,
        double[] xs,
        double[] ys,
        double[] zs)
        : this(
            shape ?? throw new ArgumentNullException(nameof(shape)),
            new ArrayDoubleList(CopyToSize(xs, RequireShape(shape).GetXSize() + 1)),
            new ArrayDoubleList(CopyToSize(ys, RequireShape(shape).GetYSize() + 1)),
            new ArrayDoubleList(CopyToSize(zs, RequireShape(shape).GetZSize() + 1)))
    {
    }

    public ArrayVoxelShape(
        DiscreteVoxelShape shape,
        DoubleList xs,
        DoubleList ys,
        DoubleList zs)
        : base(shape)
    {
        ArgumentNullException.ThrowIfNull(xs);
        ArgumentNullException.ThrowIfNull(ys);
        ArgumentNullException.ThrowIfNull(zs);

        int expectedX = shape.GetXSize() + 1;
        int expectedY = shape.GetYSize() + 1;
        int expectedZ = shape.GetZSize() + 1;
        if (expectedX != xs.Count || expectedY != ys.Count || expectedZ != zs.Count)
        {
            throw new ArgumentException(
                "Lengths of point arrays must be consistent with the size of the VoxelShape.");
        }

        _xs = xs;
        _ys = ys;
        _zs = zs;
    }

    public override DoubleList GetCoords(Direction.Axis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        if (ReferenceEquals(axis, Direction.Axis.X)) return _xs;
        if (ReferenceEquals(axis, Direction.Axis.Y)) return _ys;
        return _zs;
    }

    private static DiscreteVoxelShape RequireShape(DiscreteVoxelShape? shape) =>
        shape ?? throw new ArgumentNullException(nameof(shape));

    private static double[] CopyToSize(double[] values, int size)
    {
        ArgumentNullException.ThrowIfNull(values);
        return values.Take(size).Concat(Enumerable.Repeat(0.0, Math.Max(0, size - values.Length))).ToArray();
    }
}

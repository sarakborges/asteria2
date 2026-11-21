using Asteria.Core;

namespace Asteria.Core.Shapes;

/// <summary>
/// A one-cell-thick face slice of another voxel shape.
/// </summary>
public sealed class SliceShape : VoxelShape
{
    private static readonly DoubleList SliceCoordinates = new CubePointRange(1);

    private readonly VoxelShape _delegate;
    private readonly Direction.Axis _axis;

    public SliceShape(VoxelShape delegateShape, Direction.Axis axis, int point)
        : base(MakeSlice(
            (delegateShape ?? throw new ArgumentNullException(nameof(delegateShape))).Shape,
            axis,
            point))
    {
        _delegate = delegateShape;
        _axis = axis ?? throw new ArgumentNullException(nameof(axis));
    }

    private static DiscreteVoxelShape MakeSlice(
        DiscreteVoxelShape shape,
        Direction.Axis axis,
        int point) =>
        new SubShape(
            shape,
            axis.Choose(point, 0, 0),
            axis.Choose(0, point, 0),
            axis.Choose(0, 0, point),
            axis.Choose(point + 1, shape.GetXSize(), shape.GetXSize()),
            axis.Choose(shape.GetYSize(), point + 1, shape.GetYSize()),
            axis.Choose(shape.GetZSize(), shape.GetZSize(), point + 1));

    public override DoubleList GetCoords(Direction.Axis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        return ReferenceEquals(axis, _axis)
            ? SliceCoordinates
            : _delegate.GetCoords(axis);
    }
}

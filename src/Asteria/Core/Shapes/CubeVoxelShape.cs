using Asteria.Core;

namespace Asteria.Core.Shapes;

public sealed class CubeVoxelShape : VoxelShape
{
    public CubeVoxelShape(DiscreteVoxelShape shape)
        : base(shape)
    {
    }

    public override DoubleList GetCoords(Direction.Axis axis) =>
        new CubePointRange(Shape.GetSize(axis));

    protected override int FindIndex(Direction.Axis axis, double coordinate)
    {
        int size = Shape.GetSize(axis);
        return Mth.Floor(Mth.Clamp(coordinate * size, -1.0, size));
    }
}

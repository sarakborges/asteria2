using Asteria.Core;
using Asteria.Core.Mathematics;

namespace Asteria.Core.Shapes;

/// <summary>
/// A three-dimensional grid of occupied cells. Coordinates address cells rather than vertices.
/// </summary>
public abstract class DiscreteVoxelShape
{
    protected readonly int XSize;
    protected readonly int YSize;
    protected readonly int ZSize;

    protected DiscreteVoxelShape(int xSize, int ySize, int zSize)
    {
        if (xSize < 0 || ySize < 0 || zSize < 0)
        {
            throw new ArgumentException(
                $"Need all positive sizes: x: {xSize}, y: {ySize}, z: {zSize}");
        }

        XSize = xSize;
        YSize = ySize;
        ZSize = zSize;
    }

    public int GetXSize() => XSize;
    public int GetYSize() => YSize;
    public int GetZSize() => ZSize;

    public int GetSize(Direction.Axis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        if (ReferenceEquals(axis, Direction.Axis.X)) return XSize;
        if (ReferenceEquals(axis, Direction.Axis.Y)) return YSize;
        return ZSize;
    }

    /// <summary>
    /// Returns a copy of this occupancy grid transformed by the requested signed axis permutation.
    /// </summary>
    public DiscreteVoxelShape Rotate(OctahedralGroup rotation)
    {
        ArgumentNullException.ThrowIfNull(rotation);
        if (ReferenceEquals(rotation, OctahedralGroup.IDENTITY))
            return this;

        var rotatedSize = rotation.Rotate(new MutableVec3i(XSize, YSize, ZSize));
        int shiftX = FixupCoordinate(rotatedSize, 0);
        int shiftY = FixupCoordinate(rotatedSize, 1);
        int shiftZ = FixupCoordinate(rotatedSize, 2);

        var result = new BitSetDiscreteVoxelShape(
            rotatedSize.GetX(),
            rotatedSize.GetY(),
            rotatedSize.GetZ());

        var position = new MutableVec3i();
        for (int x = 0; x < XSize; x++)
        for (int y = 0; y < YSize; y++)
        for (int z = 0; z < ZSize; z++)
        {
            if (!IsFull(x, y, z))
                continue;

            rotation.Rotate(position.Set(x, y, z));
            result.Fill(
                unchecked(shiftX + position.GetX()),
                unchecked(shiftY + position.GetY()),
                unchecked(shiftZ + position.GetZ()));
        }

        return result;
    }

    private static int FixupCoordinate(MutableVec3i vector, int component)
    {
        int value = vector.Get(component);
        if (value < 0)
        {
            vector.SetComponent(component, unchecked(-value));
            return unchecked(-value - 1);
        }

        return 0;
    }

    public abstract bool IsFull(int x, int y, int z);

    public abstract void Fill(int x, int y, int z);

    public abstract bool IsEmpty();

    public virtual int FirstFull(Direction.Axis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        return FirstFull(axis, 0, 0);
    }

    public virtual int LastFull(Direction.Axis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        return LastFull(axis, 0, 0);
    }

    public int FirstFull(Direction.Axis axis, int b, int c)
    {
        ArgumentNullException.ThrowIfNull(axis);

        int aSize = GetSize(axis);
        if (b >= 0 && c >= 0)
        {
            Direction.Axis bAxis = AxisCycle.Forward.Cycle(axis);
            Direction.Axis cAxis = AxisCycle.Backward.Cycle(axis);
            if (b < GetSize(bAxis) && c < GetSize(cAxis))
            {
                AxisCycle transform = AxisCycleExtensions.Between(Direction.Axis.X, axis);
                for (int a = 0; a < aSize; a++)
                {
                    if (IsFull(transform, a, b, c))
                        return a;
                }
            }
        }

        return aSize;
    }

    public int LastFull(Direction.Axis axis, int b, int c)
    {
        ArgumentNullException.ThrowIfNull(axis);
        if (b >= 0 && c >= 0)
        {
            Direction.Axis bAxis = AxisCycle.Forward.Cycle(axis);
            Direction.Axis cAxis = AxisCycle.Backward.Cycle(axis);
            if (b < GetSize(bAxis) && c < GetSize(cAxis))
            {
                int aSize = GetSize(axis);
                AxisCycle transform = AxisCycleExtensions.Between(Direction.Axis.X, axis);
                for (int a = aSize - 1; a >= 0; a--)
                {
                    if (IsFull(transform, a, b, c))
                        return a + 1;
                }
            }
        }

        return 0;
    }

    public bool IsFullWide(int x, int y, int z) =>
        x >= 0 && y >= 0 && z >= 0 &&
        x < XSize && y < YSize && z < ZSize &&
        IsFull(x, y, z);

    public bool IsFull(AxisCycle transform, int x, int y, int z)
    {
        (x, y, z) = transform.Cycle(x, y, z);
        return IsFull(x, y, z);
    }

    public bool IsFullWide(AxisCycle transform, int x, int y, int z)
    {
        (x, y, z) = transform.Cycle(x, y, z);
        return IsFullWide(x, y, z);
    }

}

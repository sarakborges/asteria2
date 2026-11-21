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

    public virtual bool IsEmpty() =>
        FirstFull(Direction.Axis.X) >= LastFull(Direction.Axis.X) ||
        FirstFull(Direction.Axis.Y) >= LastFull(Direction.Axis.Y) ||
        FirstFull(Direction.Axis.Z) >= LastFull(Direction.Axis.Z);

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

    public bool IsFull(AxisCycle transform, int x, int y, int z) =>
        IsFull(
            transform.Cycle(x, y, z, Direction.Axis.X),
            transform.Cycle(x, y, z, Direction.Axis.Y),
            transform.Cycle(x, y, z, Direction.Axis.Z));

    public bool IsFullWide(AxisCycle transform, int x, int y, int z) =>
        IsFullWide(
            transform.Cycle(x, y, z, Direction.Axis.X),
            transform.Cycle(x, y, z, Direction.Axis.Y),
            transform.Cycle(x, y, z, Direction.Axis.Z));

    public void ForAllEdges(Action<int, int, int, int, int, int> consumer, bool mergeNeighbors)
    {
        ForAllAxisEdges(consumer, AxisCycle.None, mergeNeighbors);
        ForAllAxisEdges(consumer, AxisCycle.Forward, mergeNeighbors);
        ForAllAxisEdges(consumer, AxisCycle.Backward, mergeNeighbors);
    }

    private void ForAllAxisEdges(
        Action<int, int, int, int, int, int> consumer,
        AxisCycle transform,
        bool mergeNeighbors)
    {
        AxisCycle inverse = transform.Inverse();
        int aSize = GetSize(inverse.Cycle(Direction.Axis.X));
        int bSize = GetSize(inverse.Cycle(Direction.Axis.Y));
        int cSize = GetSize(inverse.Cycle(Direction.Axis.Z));

        for (int a = 0; a <= aSize; a++)
        {
            for (int b = 0; b <= bSize; b++)
            {
                int lastStart = -1;

                for (int c = 0; c <= cSize; c++)
                {
                    int fullSectors = 0;
                    int oddSectors = 0;

                    for (int da = 0; da <= 1; da++)
                    {
                        for (int db = 0; db <= 1; db++)
                        {
                            if (IsFullWide(inverse, a + da - 1, b + db - 1, c))
                            {
                                fullSectors++;
                                oddSectors ^= da ^ db;
                            }
                        }
                    }

                    if (fullSectors == 1 ||
                        fullSectors == 3 ||
                        fullSectors == 2 && (oddSectors & 1) == 0)
                    {
                        if (mergeNeighbors)
                        {
                            if (lastStart == -1)
                                lastStart = c;
                        }
                        else
                        {
                            consumer(
                                inverse.Cycle(a, b, c, Direction.Axis.X),
                                inverse.Cycle(a, b, c, Direction.Axis.Y),
                                inverse.Cycle(a, b, c, Direction.Axis.Z),
                                inverse.Cycle(a, b, c + 1, Direction.Axis.X),
                                inverse.Cycle(a, b, c + 1, Direction.Axis.Y),
                                inverse.Cycle(a, b, c + 1, Direction.Axis.Z));
                        }
                    }
                    else if (lastStart != -1)
                    {
                        consumer(
                            inverse.Cycle(a, b, lastStart, Direction.Axis.X),
                            inverse.Cycle(a, b, lastStart, Direction.Axis.Y),
                            inverse.Cycle(a, b, lastStart, Direction.Axis.Z),
                            inverse.Cycle(a, b, c, Direction.Axis.X),
                            inverse.Cycle(a, b, c, Direction.Axis.Y),
                            inverse.Cycle(a, b, c, Direction.Axis.Z));
                        lastStart = -1;
                    }
                }
            }
        }
    }

    public void ForAllFaces(Action<Direction, int, int, int> consumer)
    {
        ForAllAxisFaces(consumer, AxisCycle.None);
        ForAllAxisFaces(consumer, AxisCycle.Forward);
        ForAllAxisFaces(consumer, AxisCycle.Backward);
    }

    private void ForAllAxisFaces(Action<Direction, int, int, int> consumer, AxisCycle transform)
    {
        AxisCycle inverse = transform.Inverse();
        Direction.Axis cAxis = inverse.Cycle(Direction.Axis.Z);
        int aSize = GetSize(inverse.Cycle(Direction.Axis.X));
        int bSize = GetSize(inverse.Cycle(Direction.Axis.Y));
        int cSize = GetSize(cAxis);
        Direction negative = Direction.FromAxisAndDirection(cAxis, Direction.AxisDirection.NEGATIVE);
        Direction positive = Direction.FromAxisAndDirection(cAxis, Direction.AxisDirection.POSITIVE);

        for (int a = 0; a < aSize; a++)
        {
            for (int b = 0; b < bSize; b++)
            {
                bool lastFull = false;

                for (int c = 0; c <= cSize; c++)
                {
                    bool full = c != cSize && IsFull(inverse, a, b, c);

                    if (!lastFull && full)
                    {
                        consumer(
                            negative,
                            inverse.Cycle(a, b, c, Direction.Axis.X),
                            inverse.Cycle(a, b, c, Direction.Axis.Y),
                            inverse.Cycle(a, b, c, Direction.Axis.Z));
                    }

                    if (lastFull && !full)
                    {
                        consumer(
                            positive,
                            inverse.Cycle(a, b, c - 1, Direction.Axis.X),
                            inverse.Cycle(a, b, c - 1, Direction.Axis.Y),
                            inverse.Cycle(a, b, c - 1, Direction.Axis.Z));
                    }

                    lastFull = full;
                }
            }
        }
    }

    public void ForAllBoxes(Action<int, int, int, int, int, int> consumer, bool mergeNeighbors) =>
        BitSetDiscreteVoxelShape.ForAllBoxes(this, consumer, mergeNeighbors);
}

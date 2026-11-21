using Asteria.Core;

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
        int size = GetSize(axis);

        for (int i = 0; i < size; i++)
        {
            if (IsFullAlong(axis, i, b, c))
                return i;
        }

        return size;
    }

    public int LastFull(Direction.Axis axis, int b, int c)
    {
        ArgumentNullException.ThrowIfNull(axis);
        for (int i = GetSize(axis) - 1; i >= 0; i--)
        {
            if (IsFullAlong(axis, i, b, c))
                return i + 1;
        }

        return 0;
    }

    public bool IsFullWide(int x, int y, int z) =>
        x >= 0 && y >= 0 && z >= 0 &&
        x < XSize && y < YSize && z < ZSize &&
        IsFull(x, y, z);

    private bool IsFullAlong(Direction.Axis axis, int a, int b, int c)
    {
        if (ReferenceEquals(axis, Direction.Axis.X))
            return IsFull(a, b, c);
        if (ReferenceEquals(axis, Direction.Axis.Y))
            return IsFull(b, a, c);
        return IsFull(b, c, a);
    }
}

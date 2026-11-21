using Asteria.Core;
using System.Collections;

namespace Asteria.Core.Shapes;

/// <summary>
/// Bit-backed discrete voxel occupancy with cached occupied bounds.
/// The linear cell order is X, then Y, with Z as the least-significant coordinate.
/// </summary>
public sealed class BitSetDiscreteVoxelShape : DiscreteVoxelShape
{
    private readonly BitArray _storage;
    private int _xMin;
    private int _yMin;
    private int _zMin;
    private int _xMax;
    private int _yMax;
    private int _zMax;

    public BitSetDiscreteVoxelShape(int xSize, int ySize, int zSize)
        : base(xSize, ySize, zSize)
    {
        _storage = new BitArray(checked(checked(xSize * ySize) * zSize));
        _xMin = xSize;
        _yMin = ySize;
        _zMin = zSize;
    }

    public BitSetDiscreteVoxelShape(DiscreteVoxelShape voxelShape)
        : base(
            (voxelShape ?? throw new ArgumentNullException(nameof(voxelShape))).GetXSize(),
            voxelShape.GetYSize(),
            voxelShape.GetZSize())
    {
        _storage = new BitArray(checked(checked(XSize * YSize) * ZSize));

        if (voxelShape is BitSetDiscreteVoxelShape bitSet)
        {
            _storage = new BitArray(bitSet._storage);
        }
        else
        {
            for (int x = 0; x < XSize; x++)
            for (int y = 0; y < YSize; y++)
            for (int z = 0; z < ZSize; z++)
            {
                if (voxelShape.IsFull(x, y, z))
                    _storage[GetIndex(x, y, z)] = true;
            }
        }

        _xMin = voxelShape.FirstFull(Direction.Axis.X);
        _yMin = voxelShape.FirstFull(Direction.Axis.Y);
        _zMin = voxelShape.FirstFull(Direction.Axis.Z);
        _xMax = voxelShape.LastFull(Direction.Axis.X);
        _yMax = voxelShape.LastFull(Direction.Axis.Y);
        _zMax = voxelShape.LastFull(Direction.Axis.Z);
    }

    public static BitSetDiscreteVoxelShape WithFilledBounds(
        int xSize, int ySize, int zSize,
        int xMin, int yMin, int zMin,
        int xMax, int yMax, int zMax)
    {
        var shape = new BitSetDiscreteVoxelShape(xSize, ySize, zSize)
        {
            _xMin = xMin,
            _yMin = yMin,
            _zMin = zMin,
            _xMax = xMax,
            _yMax = yMax,
            _zMax = zMax
        };

        for (int x = xMin; x < xMax; x++)
        for (int y = yMin; y < yMax; y++)
        for (int z = zMin; z < zMax; z++)
            shape.FillUpdateBounds(x, y, z, updateBounds: false);

        return shape;
    }

    private int GetIndex(int x, int y, int z) =>
        checked(checked(x * YSize + y) * ZSize + z);

    public override bool IsFull(int x, int y, int z) =>
        _storage[GetIndex(x, y, z)];

    public override void Fill(int x, int y, int z) =>
        FillUpdateBounds(x, y, z, updateBounds: true);

    private void FillUpdateBounds(int x, int y, int z, bool updateBounds)
    {
        _storage[GetIndex(x, y, z)] = true;
        if (!updateBounds)
            return;

        _xMin = Math.Min(_xMin, x);
        _yMin = Math.Min(_yMin, y);
        _zMin = Math.Min(_zMin, z);
        _xMax = Math.Max(_xMax, x + 1);
        _yMax = Math.Max(_yMax, y + 1);
        _zMax = Math.Max(_zMax, z + 1);
    }

    public override bool IsEmpty()
    {
        foreach (bool occupied in _storage)
        {
            if (occupied)
                return false;
        }

        return true;
    }

    public override int FirstFull(Direction.Axis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        return axis.Choose(_xMin, _yMin, _zMin);
    }

    public override int LastFull(Direction.Axis axis)
    {
        ArgumentNullException.ThrowIfNull(axis);
        return axis.Choose(_xMax, _yMax, _zMax);
    }

    public bool IsInterior(int x, int y, int z)
    {
        bool interior =
            x > 0 && x < XSize - 1 &&
            y > 0 && y < YSize - 1 &&
            z > 0 && z < ZSize - 1;

        return interior &&
            IsFull(x, y, z) &&
            IsFull(x - 1, y, z) &&
            IsFull(x + 1, y, z) &&
            IsFull(x, y - 1, z) &&
            IsFull(x, y + 1, z) &&
            IsFull(x, y, z - 1) &&
            IsFull(x, y, z + 1);
    }

    public static void ForAllBoxes(
        DiscreteVoxelShape voxelShape,
        Action<int, int, int, int, int, int> consumer,
        bool mergeNeighbors)
    {
        ArgumentNullException.ThrowIfNull(voxelShape);
        ArgumentNullException.ThrowIfNull(consumer);

        var shape = new BitSetDiscreteVoxelShape(voxelShape);

        for (int y = 0; y < shape.YSize; y++)
        for (int x = 0; x < shape.XSize; x++)
        {
            int lastStartZ = -1;

            for (int z = 0; z <= shape.ZSize; z++)
            {
                if (shape.IsFullWide(x, y, z))
                {
                    if (mergeNeighbors)
                    {
                        if (lastStartZ == -1)
                            lastStartZ = z;
                    }
                    else
                    {
                        consumer(x, y, z, x + 1, y + 1, z + 1);
                    }
                }
                else if (lastStartZ != -1)
                {
                    int endX = x;
                    int endY = y;
                    shape.ClearZStrip(lastStartZ, z, x, y);

                    while (shape.IsZStripFull(lastStartZ, z, endX + 1, y))
                    {
                        shape.ClearZStrip(lastStartZ, z, endX + 1, y);
                        endX++;
                    }

                    while (shape.IsXZRectangleFull(x, endX + 1, lastStartZ, z, endY + 1))
                    {
                        for (int currentX = x; currentX <= endX; currentX++)
                            shape.ClearZStrip(lastStartZ, z, currentX, endY + 1);

                        endY++;
                    }

                    consumer(x, y, lastStartZ, endX + 1, endY + 1, z);
                    lastStartZ = -1;
                }
            }
        }
    }

    private bool IsZStripFull(int startZ, int endZ, int x, int y)
    {
        if (x >= XSize || y >= YSize)
            return false;

        int start = GetIndex(x, y, startZ);
        int end = GetIndex(x, y, endZ);
        for (int index = start; index < end; index++)
        {
            if (!_storage[index])
                return false;
        }

        return true;
    }

    private bool IsXZRectangleFull(
        int startX, int endX, int startZ, int endZ, int y)
    {
        for (int x = startX; x < endX; x++)
        {
            if (!IsZStripFull(startZ, endZ, x, y))
                return false;
        }

        return true;
    }

    private void ClearZStrip(int startZ, int endZ, int x, int y)
    {
        int start = GetIndex(x, y, startZ);
        int end = GetIndex(x, y, endZ);
        for (int index = start; index < end; index++)
            _storage[index] = false;
    }
}

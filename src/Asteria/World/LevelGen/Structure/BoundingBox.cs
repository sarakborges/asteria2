using System.Diagnostics;
using Asteria.Core;
using Asteria.World;

namespace Asteria.World.LevelGen.Structure;

/// <summary>
/// Inclusive integer world bounds used by Minecraft structure generation and density volumes.
/// Geometry operations mirror the Minecraft Java 26.3 BoundingBox implementation.
/// </summary>
public class BoundingBox : IEquatable<BoundingBox>
{
    private int _minX;
    private int _minY;
    private int _minZ;
    private int _maxX;
    private int _maxY;
    private int _maxZ;

    public BoundingBox(BlockPos position)
        : this(
            position?.GetX() ?? throw new ArgumentNullException(nameof(position)),
            position.GetY(),
            position.GetZ(),
            position.GetX(),
            position.GetY(),
            position.GetZ())
    {
    }

    public BoundingBox(int minX, int minY, int minZ, int maxX, int maxY, int maxZ)
    {
        _minX = minX;
        _minY = minY;
        _minZ = minZ;
        _maxX = maxX;
        _maxY = maxY;
        _maxZ = maxZ;

        if (maxX < minX || maxY < minY || maxZ < minZ)
        {
            Trace.TraceWarning("Invalid bounding box data, inverted bounds for: {0}", this);
            _minX = Math.Min(minX, maxX);
            _minY = Math.Min(minY, maxY);
            _minZ = Math.Min(minZ, maxZ);
            _maxX = Math.Max(minX, maxX);
            _maxY = Math.Max(minY, maxY);
            _maxZ = Math.Max(minZ, maxZ);
        }
    }

    public static BoundingBox FromCorners(Vec3i first, Vec3i second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return new BoundingBox(
            Math.Min(first.GetX(), second.GetX()),
            Math.Min(first.GetY(), second.GetY()),
            Math.Min(first.GetZ(), second.GetZ()),
            Math.Max(first.GetX(), second.GetX()),
            Math.Max(first.GetY(), second.GetY()),
            Math.Max(first.GetZ(), second.GetZ()));
    }

    public static BoundingBox Infinite() =>
        new(int.MinValue, int.MinValue, int.MinValue, int.MaxValue, int.MaxValue, int.MaxValue);

    public static BoundingBox OrientBox(
        int footX,
        int footY,
        int footZ,
        int offX,
        int offY,
        int offZ,
        int width,
        int height,
        int depth,
        Direction direction)
    {
        ArgumentNullException.ThrowIfNull(direction);

        if (ReferenceEquals(direction, Direction.NORTH))
        {
            return new BoundingBox(
                unchecked(footX + offX),
                unchecked(footY + offY),
                unchecked(footZ - depth + 1 + offZ),
                unchecked(footX + width - 1 + offX),
                unchecked(footY + height - 1 + offY),
                unchecked(footZ + offZ));
        }

        if (ReferenceEquals(direction, Direction.WEST))
        {
            return new BoundingBox(
                unchecked(footX - depth + 1 + offZ),
                unchecked(footY + offY),
                unchecked(footZ + offX),
                unchecked(footX + offZ),
                unchecked(footY + height - 1 + offY),
                unchecked(footZ + width - 1 + offX));
        }

        if (ReferenceEquals(direction, Direction.EAST))
        {
            return new BoundingBox(
                unchecked(footX + offZ),
                unchecked(footY + offY),
                unchecked(footZ + offX),
                unchecked(footX + depth - 1 + offZ),
                unchecked(footY + height - 1 + offY),
                unchecked(footZ + width - 1 + offX));
        }

        // SOUTH and the vertical directions follow the reference switch's default branch.
        return new BoundingBox(
            unchecked(footX + offX),
            unchecked(footY + offY),
            unchecked(footZ + offZ),
            unchecked(footX + width - 1 + offX),
            unchecked(footY + height - 1 + offY),
            unchecked(footZ + depth - 1 + offZ));
    }

    public IEnumerable<ChunkPos> IntersectingChunks()
    {
        int minChunkX = SectionPos.BlockToSectionCoord(_minX);
        int minChunkZ = SectionPos.BlockToSectionCoord(_minZ);
        int maxChunkX = SectionPos.BlockToSectionCoord(_maxX);
        int maxChunkZ = SectionPos.BlockToSectionCoord(_maxZ);
        return ChunkPos.RangeClosed(new ChunkPos(minChunkX, minChunkZ), new ChunkPos(maxChunkX, maxChunkZ));
    }

    public bool Intersects(BoundingBox other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return Intersects(other._minX, other._minY, other._minZ, other._maxX, other._maxY, other._maxZ);
    }

    public bool Intersects(int minX, int minY, int minZ, int maxX, int maxY, int maxZ) =>
        _maxX >= minX && _minX <= maxX &&
        _maxZ >= minZ && _minZ <= maxZ &&
        _maxY >= minY && _minY <= maxY;

    public bool Intersects(int minX, int minZ, int maxX, int maxZ) =>
        _maxX >= minX && _minX <= maxX &&
        _maxZ >= minZ && _minZ <= maxZ;

    public static BoundingBox? EncapsulatingPositions(IEnumerable<BlockPos> positions)
    {
        ArgumentNullException.ThrowIfNull(positions);
        using IEnumerator<BlockPos> iterator = positions.GetEnumerator();
        if (!iterator.MoveNext())
            return null;

        var result = new BoundingBox(iterator.Current);
        while (iterator.MoveNext())
            result.Encapsulate(iterator.Current);
        return result;
    }

    public static BoundingBox? EncapsulatingBoxes(IEnumerable<BoundingBox> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        using IEnumerator<BoundingBox> iterator = boxes.GetEnumerator();
        if (!iterator.MoveNext())
            return null;

        BoundingBox first = iterator.Current;
        var result = new BoundingBox(
            first._minX, first._minY, first._minZ,
            first._maxX, first._maxY, first._maxZ);
        while (iterator.MoveNext())
            result.Encapsulate(iterator.Current);
        return result;
    }

    public BoundingBox Encapsulate(BoundingBox other)
    {
        ArgumentNullException.ThrowIfNull(other);
        _minX = Math.Min(_minX, other._minX);
        _minY = Math.Min(_minY, other._minY);
        _minZ = Math.Min(_minZ, other._minZ);
        _maxX = Math.Max(_maxX, other._maxX);
        _maxY = Math.Max(_maxY, other._maxY);
        _maxZ = Math.Max(_maxZ, other._maxZ);
        return this;
    }

    public static BoundingBox Encapsulating(BoundingBox first, BoundingBox second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);
        return new BoundingBox(
            Math.Min(first._minX, second._minX),
            Math.Min(first._minY, second._minY),
            Math.Min(first._minZ, second._minZ),
            Math.Max(first._maxX, second._maxX),
            Math.Max(first._maxY, second._maxY),
            Math.Max(first._maxZ, second._maxZ));
    }

    public BoundingBox Encapsulate(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        _minX = Math.Min(_minX, position.GetX());
        _minY = Math.Min(_minY, position.GetY());
        _minZ = Math.Min(_minZ, position.GetZ());
        _maxX = Math.Max(_maxX, position.GetX());
        _maxY = Math.Max(_maxY, position.GetY());
        _maxZ = Math.Max(_maxZ, position.GetZ());
        return this;
    }

    public BoundingBox Move(int dx, int dy, int dz)
    {
        _minX = unchecked(_minX + dx);
        _minY = unchecked(_minY + dy);
        _minZ = unchecked(_minZ + dz);
        _maxX = unchecked(_maxX + dx);
        _maxY = unchecked(_maxY + dy);
        _maxZ = unchecked(_maxZ + dz);
        return this;
    }

    public BoundingBox Move(Vec3i amount)
    {
        ArgumentNullException.ThrowIfNull(amount);
        return Move(amount.GetX(), amount.GetY(), amount.GetZ());
    }

    public BoundingBox Moved(int dx, int dy, int dz) =>
        new(
            unchecked(_minX + dx), unchecked(_minY + dy), unchecked(_minZ + dz),
            unchecked(_maxX + dx), unchecked(_maxY + dy), unchecked(_maxZ + dz));

    public BoundingBox InflatedBy(int amount) =>
        InflatedBy(amount, amount, amount);

    public BoundingBox InflatedBy(int inflateX, int inflateY, int inflateZ) =>
        new(
            unchecked(_minX - inflateX),
            unchecked(_minY - inflateY),
            unchecked(_minZ - inflateZ),
            unchecked(_maxX + inflateX),
            unchecked(_maxY + inflateY),
            unchecked(_maxZ + inflateZ));

    public bool IsInside(Vec3i position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return IsInside(position.GetX(), position.GetY(), position.GetZ());
    }

    public bool IsInside(int x, int y, int z) =>
        x >= _minX && x <= _maxX &&
        z >= _minZ && z <= _maxZ &&
        y >= _minY && y <= _maxY;

    public Vec3i GetLength() =>
        new(unchecked(_maxX - _minX), unchecked(_maxY - _minY), unchecked(_maxZ - _minZ));

    public int GetXSpan() => unchecked(_maxX - _minX + 1);

    public int GetYSpan() => unchecked(_maxY - _minY + 1);

    public int GetZSpan() => unchecked(_maxZ - _minZ + 1);

    public BlockPos GetCenter() =>
        new(
            unchecked(_minX + unchecked(_maxX - _minX + 1) / 2),
            unchecked(_minY + unchecked(_maxY - _minY + 1) / 2),
            unchecked(_minZ + unchecked(_maxZ - _minZ + 1) / 2));

    public void ForAllCorners(Action<BlockPos> consumer)
    {
        ArgumentNullException.ThrowIfNull(consumer);
        var position = new BlockPos.MutableBlockPos();
        consumer(position.Set(_maxX, _maxY, _maxZ));
        consumer(position.Set(_minX, _maxY, _maxZ));
        consumer(position.Set(_maxX, _minY, _maxZ));
        consumer(position.Set(_minX, _minY, _maxZ));
        consumer(position.Set(_maxX, _maxY, _minZ));
        consumer(position.Set(_minX, _maxY, _minZ));
        consumer(position.Set(_maxX, _minY, _minZ));
        consumer(position.Set(_minX, _minY, _minZ));
    }

    public int MinX() => _minX;
    public int MinY() => _minY;
    public int MinZ() => _minZ;
    public int MaxX() => _maxX;
    public int MaxY() => _maxY;
    public int MaxZ() => _maxZ;

    public bool Equals(BoundingBox? other) =>
        ReferenceEquals(this, other) ||
        (other is not null &&
         _minX == other._minX && _minY == other._minY && _minZ == other._minZ &&
         _maxX == other._maxX && _maxY == other._maxY && _maxZ == other._maxZ);

    public override bool Equals(object? obj) =>
        obj is BoundingBox other && Equals(other);

    public override int GetHashCode()
    {
        int hash = 1;
        hash = unchecked(hash * 31 + _minX);
        hash = unchecked(hash * 31 + _minY);
        hash = unchecked(hash * 31 + _minZ);
        hash = unchecked(hash * 31 + _maxX);
        hash = unchecked(hash * 31 + _maxY);
        hash = unchecked(hash * 31 + _maxZ);
        return hash;
    }

    public override string ToString() =>
        $"BoundingBox{{minX={_minX}, minY={_minY}, minZ={_minZ}, maxX={_maxX}, maxY={_maxY}, maxZ={_maxZ}}}";
}

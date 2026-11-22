using Asteria.Serialization;

namespace Asteria.Core;

/// <summary>
/// Integer block coordinates. Packed layout follows the reference 26/12/26-bit representation.
/// </summary>
public class BlockPos : Vec3i
{
    /// <summary>Exactly three signed integer coordinates in X/Y/Z order.</summary>
    public static Codec<BlockPos> CODEC { get; } =
        Codecs.INT.ListOf().FlatXMap(
            static values => values.Count == 3
                ? DataResult<BlockPos>.Success(new BlockPos(values[0], values[1], values[2]))
                : DataResult<BlockPos>.Error(() => "Input is not a list of 3 elements"),
            static position => DataResult<IReadOnlyList<int>>.Success(
                new[] { position.GetX(), position.GetY(), position.GetZ() }));

    public const int PackedHorizontalLength = 26;
    public const int PackedYLength = 12;
    public const int MaxHorizontalCoordinate = (1 << PackedHorizontalLength) / 2 - 1;

    private const int YOffset = 0;
    private const int ZOffset = PackedYLength;
    private const int XOffset = PackedYLength + PackedHorizontalLength;

    private const long PackedXMask = (1L << PackedHorizontalLength) - 1L;
    private const long PackedYMask = (1L << PackedYLength) - 1L;
    private const long PackedZMask = (1L << PackedHorizontalLength) - 1L;

    public new static readonly BlockPos Zero = new(0, 0, 0);

    public BlockPos(int x, int y, int z)
        : base(x, y, z)
    {
    }

    public static int GetX(long blockNode) =>
        unchecked((int)(blockNode >> XOffset));

    public static int GetY(long blockNode) =>
        unchecked((int)(blockNode << (64 - PackedYLength) >> (64 - PackedYLength)));

    public static int GetZ(long blockNode) =>
        unchecked((int)(blockNode << (64 - ZOffset - PackedHorizontalLength) >> (64 - PackedHorizontalLength)));

    public static BlockPos Of(long blockNode) =>
        new(GetX(blockNode), GetY(blockNode), GetZ(blockNode));

    public static BlockPos Containing(double x, double y, double z) =>
        new(FloorToInt(x), FloorToInt(y), FloorToInt(z));

    public static BlockPos Min(BlockPos a, BlockPos b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return new BlockPos(
            Math.Min(a.GetX(), b.GetX()),
            Math.Min(a.GetY(), b.GetY()),
            Math.Min(a.GetZ(), b.GetZ()));
    }

    public static BlockPos Max(BlockPos a, BlockPos b)
    {
        ArgumentNullException.ThrowIfNull(a);
        ArgumentNullException.ThrowIfNull(b);
        return new BlockPos(
            Math.Max(a.GetX(), b.GetX()),
            Math.Max(a.GetY(), b.GetY()),
            Math.Max(a.GetZ(), b.GetZ()));
    }

    public long AsLong() =>
        AsLong(GetX(), GetY(), GetZ());

    public static long AsLong(int x, int y, int z)
    {
        long node = 0L;
        node |= ((long)x & PackedXMask) << XOffset;
        node |= ((long)y & PackedYMask) << YOffset;
        return node | ((long)z & PackedZMask) << ZOffset;
    }

    public static long Offset(long blockNode, int stepX, int stepY, int stepZ) =>
        AsLong(
            unchecked(GetX(blockNode) + stepX),
            unchecked(GetY(blockNode) + stepY),
            unchecked(GetZ(blockNode) + stepZ));

    public static long GetFlatIndex(long neighborBlockNode) =>
        neighborBlockNode & -16L;

    public override BlockPos Offset(int x, int y, int z) =>
        x == 0 && y == 0 && z == 0
            ? this
            : new BlockPos(
                unchecked(GetX() + x),
                unchecked(GetY() + y),
                unchecked(GetZ() + z));

    public override BlockPos Offset(Vec3i vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return Offset(vector.GetX(), vector.GetY(), vector.GetZ());
    }

    public override BlockPos Subtract(Vec3i vector)
    {
        ArgumentNullException.ThrowIfNull(vector);
        return Offset(
            unchecked(-vector.GetX()),
            unchecked(-vector.GetY()),
            unchecked(-vector.GetZ()));
    }

    public override BlockPos Multiply(int scale) =>
        scale == 1
            ? this
            : scale == 0
                ? Zero
                : new BlockPos(
                    unchecked(GetX() * scale),
                    unchecked(GetY() * scale),
                    unchecked(GetZ() * scale));

    public override BlockPos Above() => Above(1);

    public override BlockPos Above(int steps) => Offset(0, steps, 0);

    public override BlockPos Below() => Below(1);

    public override BlockPos Below(int steps) => Offset(0, unchecked(-steps), 0);

    public override BlockPos North() => North(1);

    public override BlockPos North(int steps) => Offset(0, 0, unchecked(-steps));

    public override BlockPos South() => South(1);

    public override BlockPos South(int steps) => Offset(0, 0, steps);

    public override BlockPos West() => West(1);

    public override BlockPos West(int steps) => Offset(unchecked(-steps), 0, 0);

    public override BlockPos East() => East(1);

    public override BlockPos East(int steps) => Offset(steps, 0, 0);

    public override BlockPos Relative(Axis axis, int steps) =>
        steps == 0
            ? this
            : axis switch
            {
                Axis.X => Offset(steps, 0, 0),
                Axis.Y => Offset(0, steps, 0),
                Axis.Z => Offset(0, 0, steps),
                _ => throw new ArgumentOutOfRangeException(nameof(axis))
            };

    public override BlockPos Cross(Vec3i vector)
    {
        ArgumentNullException.ThrowIfNull(vector);

        return new BlockPos(
            unchecked(GetY() * vector.GetZ() - GetZ() * vector.GetY()),
            unchecked(GetZ() * vector.GetX() - GetX() * vector.GetZ()),
            unchecked(GetX() * vector.GetY() - GetY() * vector.GetX()));
    }

    public BlockPos AtY(int y) =>
        new(GetX(), y, GetZ());

    public virtual BlockPos Immutable() => this;

    public MutableBlockPos Mutable() =>
        new(GetX(), GetY(), GetZ());

    /// <summary>
    /// Enumerates an inclusive box in X/Y/Z traversal order.
    /// The same mutable cursor is reused for each yielded position, matching the source contract.
    /// </summary>
    public static IEnumerable<BlockPos> BetweenClosed(
        int minX,
        int minY,
        int minZ,
        int maxX,
        int maxY,
        int maxZ)
    {
        if (minX > maxX || minY > maxY || minZ > maxZ)
            yield break;

        var cursor = new MutableBlockPos();
        for (int z = minZ; ; z = unchecked(z + 1))
        {
            for (int y = minY; ; y = unchecked(y + 1))
            {
                for (int x = minX; ; x = unchecked(x + 1))
                {
                    yield return cursor.Set(x, y, z);
                    if (x == maxX)
                        break;
                }

                if (y == maxY)
                    break;
            }

            if (z == maxZ)
                break;
        }
    }

    public static IEnumerable<BlockPos> BetweenClosed(BlockPos first, BlockPos second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return BetweenClosed(
            Math.Min(first.GetX(), second.GetX()),
            Math.Min(first.GetY(), second.GetY()),
            Math.Min(first.GetZ(), second.GetZ()),
            Math.Max(first.GetX(), second.GetX()),
            Math.Max(first.GetY(), second.GetY()),
            Math.Max(first.GetZ(), second.GetZ()));
    }

    private static int FloorToInt(double value)
    {
        if (double.IsNaN(value))
            return 0;

        double floor = Math.Floor(value);
        if (floor <= int.MinValue)
            return int.MinValue;
        if (floor >= int.MaxValue)
            return int.MaxValue;
        return (int)floor;
    }

    public sealed class MutableBlockPos : BlockPos
    {
        public MutableBlockPos()
            : this(0, 0, 0)
        {
        }

        public MutableBlockPos(int x, int y, int z)
            : base(x, y, z)
        {
        }

        public MutableBlockPos(double x, double y, double z)
            : this(FloorToInt(x), FloorToInt(y), FloorToInt(z))
        {
        }

        public override BlockPos Offset(int x, int y, int z) =>
            base.Offset(x, y, z).Immutable();

        public override BlockPos Offset(Vec3i vector)
        {
            ArgumentNullException.ThrowIfNull(vector);
            return Offset(vector.GetX(), vector.GetY(), vector.GetZ());
        }

        public override BlockPos Subtract(Vec3i vector) =>
            base.Subtract(vector).Immutable();

        public override BlockPos Multiply(int scale) =>
            base.Multiply(scale).Immutable();

        public override BlockPos Relative(Axis axis, int steps) =>
            base.Relative(axis, steps).Immutable();

        public MutableBlockPos Set(int x, int y, int z)
        {
            SetX(x);
            SetY(y);
            SetZ(z);
            return this;
        }

        public MutableBlockPos Set(double x, double y, double z) =>
            Set(FloorToInt(x), FloorToInt(y), FloorToInt(z));

        public MutableBlockPos Set(Vec3i vector)
        {
            ArgumentNullException.ThrowIfNull(vector);
            return Set(vector.GetX(), vector.GetY(), vector.GetZ());
        }

        public MutableBlockPos Set(long packedPosition) =>
            Set(GetX(packedPosition), GetY(packedPosition), GetZ(packedPosition));

        public MutableBlockPos SetWithOffset(Vec3i position, int x, int y, int z)
        {
            ArgumentNullException.ThrowIfNull(position);
            return Set(
                unchecked(position.GetX() + x),
                unchecked(position.GetY() + y),
                unchecked(position.GetZ() + z));
        }

        public MutableBlockPos SetWithOffset(Vec3i position, Vec3i offset)
        {
            ArgumentNullException.ThrowIfNull(offset);
            return SetWithOffset(position, offset.GetX(), offset.GetY(), offset.GetZ());
        }

        public MutableBlockPos Move(int x, int y, int z) =>
            Set(
                unchecked(GetX() + x),
                unchecked(GetY() + y),
                unchecked(GetZ() + z));

        public MutableBlockPos Move(Vec3i offset)
        {
            ArgumentNullException.ThrowIfNull(offset);
            return Move(offset.GetX(), offset.GetY(), offset.GetZ());
        }

        public MutableBlockPos Move(Axis axis, int steps) =>
            axis switch
            {
                Axis.X => Move(steps, 0, 0),
                Axis.Y => Move(0, steps, 0),
                Axis.Z => Move(0, 0, steps),
                _ => throw new ArgumentOutOfRangeException(nameof(axis))
            };

        public MutableBlockPos Clamp(Axis axis, int minimum, int maximum)
        {
            static int ClampLikeReference(int value, int min, int max) =>
                value < min ? min : value > max ? max : value;

            return axis switch
            {
                Axis.X => Set(ClampLikeReference(GetX(), minimum, maximum), GetY(), GetZ()),
                Axis.Y => Set(GetX(), ClampLikeReference(GetY(), minimum, maximum), GetZ()),
                Axis.Z => Set(GetX(), GetY(), ClampLikeReference(GetZ(), minimum, maximum)),
                _ => throw new ArgumentOutOfRangeException(nameof(axis))
            };
        }

        public new MutableBlockPos SetX(int x)
        {
            base.SetX(x);
            return this;
        }

        public new MutableBlockPos SetY(int y)
        {
            base.SetY(y);
            return this;
        }

        public new MutableBlockPos SetZ(int z)
        {
            base.SetZ(z);
            return this;
        }

        public override BlockPos Immutable() =>
            new(GetX(), GetY(), GetZ());
    }
}

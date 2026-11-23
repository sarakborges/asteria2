using Asteria.Content.Chunks;
using Asteria.World;

namespace Asteria.Core;

/// <summary>
/// Coordinates of a 16x16x16 block section, packed as 22/20/22-bit X/Y/Z values.
/// </summary>
public sealed class SectionPos : Vec3i
{
    public const int SectionBits = 4;
    public const int SectionSize = 1 << SectionBits;
    public const int SectionBlockCount = SectionSize * SectionSize * SectionSize;
    public const int SectionMask = SectionSize - 1;
    public const int SectionHalfSize = SectionSize / 2;
    public const int SectionMaxIndex = SectionMask;

    private const int PackedXLength = 22;
    private const int PackedYLength = 20;
    private const int PackedZLength = 22;

    private const long PackedXMask = (1L << PackedXLength) - 1L;
    private const long PackedYMask = (1L << PackedYLength) - 1L;
    private const long PackedZMask = (1L << PackedZLength) - 1L;

    private const int YOffset = 0;
    private const int ZOffset = PackedYLength;
    private const int XOffset = PackedYLength + PackedZLength;

    private const int RelativeXShift = 8;
    private const int RelativeYShift = 0;
    private const int RelativeZShift = 4;

    private SectionPos(int x, int y, int z)
        : base(x, y, z)
    {
    }

    public static SectionPos Of(int x, int y, int z) =>
        new(x, y, z);

    public static SectionPos Of(Position position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return new(
            BlockToSectionCoord(position.GetX()),
            BlockToSectionCoord(position.GetY()),
            BlockToSectionCoord(position.GetZ()));
    }

    public static SectionPos Of(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return new(
            BlockToSectionCoord(position.GetX()),
            BlockToSectionCoord(position.GetY()),
            BlockToSectionCoord(position.GetZ()));
    }

    public static SectionPos Of(ChunkPos position, int sectionY) =>
        new(position.X, sectionY, position.Z);

    public static SectionPos BottomOf(ChunkAccess chunk)
    {
        ArgumentNullException.ThrowIfNull(chunk);
        return Of(chunk.GetPos(), chunk.GetMinSectionY());
    }

    public static SectionPos Of(long sectionNode) =>
        new(GetX(sectionNode), GetY(sectionNode), GetZ(sectionNode));

    public static long Offset(long sectionNode, int stepX, int stepY, int stepZ) =>
        AsLong(
            unchecked(GetX(sectionNode) + stepX),
            unchecked(GetY(sectionNode) + stepY),
            unchecked(GetZ(sectionNode) + stepZ));

    public static long Offset(long sectionNode, Direction direction)
    {
        ArgumentNullException.ThrowIfNull(direction);
        return Offset(
            sectionNode,
            direction.GetStepX(),
            direction.GetStepY(),
            direction.GetStepZ());
    }

    public static int PosToSectionCoord(double position) =>
        BlockToSectionCoord(FloorToInt(position));

    public static int BlockToSectionCoord(int blockCoordinate) =>
        blockCoordinate >> SectionBits;

    public static int BlockToSectionCoord(double coordinate) =>
        FloorToInt(coordinate) >> SectionBits;

    public static int SectionRelative(int blockCoordinate) =>
        blockCoordinate & SectionMask;

    public static short SectionRelativePos(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);

        int x = SectionRelative(position.GetX());
        int y = SectionRelative(position.GetY());
        int z = SectionRelative(position.GetZ());

        return unchecked((short)(x << RelativeXShift | z << RelativeZShift | y << RelativeYShift));
    }

    public static int SectionRelativeX(short relative) =>
        (unchecked((ushort)relative) >> RelativeXShift) & SectionMask;

    public static int SectionRelativeY(short relative) =>
        (unchecked((ushort)relative) >> RelativeYShift) & SectionMask;

    public static int SectionRelativeZ(short relative) =>
        (unchecked((ushort)relative) >> RelativeZShift) & SectionMask;

    public int RelativeToBlockX(short relative) =>
        unchecked(MinBlockX() + SectionRelativeX(relative));

    public int RelativeToBlockY(short relative) =>
        unchecked(MinBlockY() + SectionRelativeY(relative));

    public int RelativeToBlockZ(short relative) =>
        unchecked(MinBlockZ() + SectionRelativeZ(relative));

    public BlockPos RelativeToBlockPos(short relative) =>
        new(
            RelativeToBlockX(relative),
            RelativeToBlockY(relative),
            RelativeToBlockZ(relative));

    public static int SectionToBlockCoord(int sectionCoordinate) =>
        unchecked(sectionCoordinate << SectionBits);

    public static int SectionToBlockCoord(int sectionCoordinate, int offset) =>
        unchecked(SectionToBlockCoord(sectionCoordinate) + offset);

    public static int GetX(long sectionNode) =>
        unchecked((int)(sectionNode >> XOffset));

    public static int GetY(long sectionNode) =>
        unchecked((int)(sectionNode << (64 - PackedYLength) >> (64 - PackedYLength)));

    public static int GetZ(long sectionNode) =>
        unchecked((int)(sectionNode << (64 - ZOffset - PackedZLength) >> (64 - PackedZLength)));

    public override SectionPos Offset(int x, int y, int z) =>
        x == 0 && y == 0 && z == 0
            ? this
            : new SectionPos(
                unchecked(GetX() + x),
                unchecked(GetY() + y),
                unchecked(GetZ() + z));

    public int MinBlockX() => SectionToBlockCoord(GetX());

    public int MinBlockY() => SectionToBlockCoord(GetY());

    public int MinBlockZ() => SectionToBlockCoord(GetZ());

    public int MaxBlockX() => SectionToBlockCoord(GetX(), SectionMaxIndex);

    public int MaxBlockY() => SectionToBlockCoord(GetY(), SectionMaxIndex);

    public int MaxBlockZ() => SectionToBlockCoord(GetZ(), SectionMaxIndex);

    public static long BlockToSection(long blockNode) =>
        AsLong(
            BlockToSectionCoord(BlockPos.GetX(blockNode)),
            BlockToSectionCoord(BlockPos.GetY(blockNode)),
            BlockToSectionCoord(BlockPos.GetZ(blockNode)));

    public static long GetZeroNode(int x, int z) =>
        GetZeroNode(AsLong(x, 0, z));

    public static long GetZeroNode(long sectionNode) =>
        sectionNode & -1_048_576L;

    public static long SectionToChunk(long sectionNode) =>
        ChunkPos.Pack(GetX(sectionNode), GetZ(sectionNode));

    public BlockPos Origin() =>
        new(
            SectionToBlockCoord(GetX()),
            SectionToBlockCoord(GetY()),
            SectionToBlockCoord(GetZ()));

    public BlockPos Center() =>
        Origin().Offset(SectionHalfSize, SectionHalfSize, SectionHalfSize);

    public ChunkPos Chunk() =>
        new(GetX(), GetZ());

    public static long AsLong(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return AsLong(
            BlockToSectionCoord(position.GetX()),
            BlockToSectionCoord(position.GetY()),
            BlockToSectionCoord(position.GetZ()));
    }

    public static long AsLong(int x, int y, int z)
    {
        long node = 0L;
        node |= ((long)x & PackedXMask) << XOffset;
        node |= ((long)y & PackedYMask) << YOffset;
        return node | ((long)z & PackedZMask) << ZOffset;
    }

    public long AsLong() =>
        AsLong(GetX(), GetY(), GetZ());

    public IEnumerable<BlockPos> BlocksInside() =>
        BlockPos.BetweenClosed(
            MinBlockX(), MinBlockY(), MinBlockZ(),
            MaxBlockX(), MaxBlockY(), MaxBlockZ());

    public static IEnumerable<SectionPos> Cube(SectionPos center, int radius)
    {
        ArgumentNullException.ThrowIfNull(center);
        return BetweenClosedStream(
            unchecked(center.GetX() - radius),
            unchecked(center.GetY() - radius),
            unchecked(center.GetZ() - radius),
            unchecked(center.GetX() + radius),
            unchecked(center.GetY() + radius),
            unchecked(center.GetZ() + radius));
    }

    public static IEnumerable<SectionPos> AroundChunk(
        ChunkPos center,
        int radius,
        int minSection,
        int maxSection) =>
        BetweenClosedStream(
            unchecked(center.X - radius),
            minSection,
            unchecked(center.Z - radius),
            unchecked(center.X + radius),
            maxSection,
            unchecked(center.Z + radius));

    /// <summary>Enumerates a closed section box with X as the fastest-changing coordinate.</summary>
    public static IEnumerable<SectionPos> BetweenClosedStream(
        int minX,
        int minY,
        int minZ,
        int maxX,
        int maxY,
        int maxZ)
    {
        if (minX > maxX || minY > maxY || minZ > maxZ)
            yield break;

        for (int z = minZ; ; z = unchecked(z + 1))
        {
            for (int y = minY; ; y = unchecked(y + 1))
            {
                for (int x = minX; ; x = unchecked(x + 1))
                {
                    yield return new SectionPos(x, y, z);
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

    public static void AroundAndAtBlockPos(BlockPos blockPosition, Action<long> sectionConsumer)
    {
        ArgumentNullException.ThrowIfNull(blockPosition);
        AroundAndAtBlockPos(blockPosition.GetX(), blockPosition.GetY(), blockPosition.GetZ(), sectionConsumer);
    }

    public static void AroundAndAtBlockPos(long blockPosition, Action<long> sectionConsumer) =>
        AroundAndAtBlockPos(
            BlockPos.GetX(blockPosition),
            BlockPos.GetY(blockPosition),
            BlockPos.GetZ(blockPosition),
            sectionConsumer);

    public static void AroundAndAtBlockPos(
        int blockX,
        int blockY,
        int blockZ,
        Action<long> sectionConsumer)
    {
        ArgumentNullException.ThrowIfNull(sectionConsumer);

        int minSectionX = BlockToSectionCoord(unchecked(blockX - 1));
        int maxSectionX = BlockToSectionCoord(unchecked(blockX + 1));
        int minSectionY = BlockToSectionCoord(unchecked(blockY - 1));
        int maxSectionY = BlockToSectionCoord(unchecked(blockY + 1));
        int minSectionZ = BlockToSectionCoord(unchecked(blockZ - 1));
        int maxSectionZ = BlockToSectionCoord(unchecked(blockZ + 1));

        if (minSectionX == maxSectionX &&
            minSectionY == maxSectionY &&
            minSectionZ == maxSectionZ)
        {
            sectionConsumer(AsLong(minSectionX, minSectionY, minSectionZ));
            return;
        }

        for (int sectionX = minSectionX; ; sectionX = unchecked(sectionX + 1))
        {
            for (int sectionY = minSectionY; ; sectionY = unchecked(sectionY + 1))
            {
                for (int sectionZ = minSectionZ; ; sectionZ = unchecked(sectionZ + 1))
                {
                    sectionConsumer(AsLong(sectionX, sectionY, sectionZ));
                    if (sectionZ == maxSectionZ)
                        break;
                }

                if (sectionY == maxSectionY)
                    break;
            }

            if (sectionX == maxSectionX)
                break;
        }
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
}

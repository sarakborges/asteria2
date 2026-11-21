using Asteria.Core;

namespace Asteria.World;

/// <summary>
/// Horizontal chunk coordinates. Packed X/Z coordinates occupy the low/high 32 bits.
/// </summary>
public readonly record struct ChunkPos(int X, int Z)
{
    private const long CoordinateMask = 0xFFFFFFFFL;

    public const int RegionBits = 5;
    public const int RegionSize = 1 << RegionBits;
    public const int RegionMask = RegionSize - 1;
    public const int RegionMaxIndex = RegionSize - 1;

    public static readonly ChunkPos Zero = new(0, 0);
    public static readonly long InvalidChunkPos = Pack(1_875_066, 1_875_066);

    public static ChunkPos Containing(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return new(
            SectionPos.BlockToSectionCoord(position.GetX()),
            SectionPos.BlockToSectionCoord(position.GetZ()));
    }

    public static ChunkPos Unpack(long key) =>
        new(GetX(key), GetZ(key));

    public static ChunkPos MinFromRegion(int regionX, int regionZ) =>
        new(unchecked(regionX << RegionBits), unchecked(regionZ << RegionBits));

    public static ChunkPos MaxFromRegion(int regionX, int regionZ) =>
        new(
            unchecked((regionX << RegionBits) + RegionMaxIndex),
            unchecked((regionZ << RegionBits) + RegionMaxIndex));

    public long Pack() => Pack(X, Z);

    public static long Pack(int x, int z) =>
        unchecked(((long)(uint)x & CoordinateMask) | (((long)(uint)z & CoordinateMask) << 32));

    public static long FromSectionNode(long sectionNode) =>
        Pack(SectionPos.GetX(sectionNode), SectionPos.GetZ(sectionNode));

    public static long Pack(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return Pack(
            SectionPos.BlockToSectionCoord(position.GetX()),
            SectionPos.BlockToSectionCoord(position.GetZ()));
    }

    public static int GetX(long position) =>
        unchecked((int)(position & CoordinateMask));

    public static int GetZ(long position) =>
        unchecked((int)((ulong)position >> 32));

    public static int Hash(int x, int z)
    {
        int xTransform = unchecked(1_664_525 * x + 1_013_904_223);
        int zTransform = unchecked(1_664_525 * (z ^ unchecked((int)0xDEADBEEF)) + 1_013_904_223);
        return xTransform ^ zTransform;
    }

    public int GetMiddleBlockX() => GetBlockX(8);

    public int GetMiddleBlockZ() => GetBlockZ(8);

    public int GetMinBlockX() => SectionPos.SectionToBlockCoord(X);

    public int GetMinBlockZ() => SectionPos.SectionToBlockCoord(Z);

    public int GetMaxBlockX() => GetBlockX(15);

    public int GetMaxBlockZ() => GetBlockZ(15);

    public int GetRegionX() => X >> RegionBits;

    public int GetRegionZ() => Z >> RegionBits;

    public static int GetRegionX(long position) => GetX(position) >> RegionBits;

    public static int GetRegionZ(long position) => GetZ(position) >> RegionBits;

    public int GetRegionLocalX() => X & RegionMask;

    public int GetRegionLocalZ() => Z & RegionMask;

    public BlockPos GetBlockAt(int x, int y, int z) =>
        new(GetBlockX(x), y, GetBlockZ(z));

    public int GetBlockX(int offset) =>
        SectionPos.SectionToBlockCoord(X, offset);

    public int GetBlockZ(int offset) =>
        SectionPos.SectionToBlockCoord(Z, offset);

    public BlockPos GetMiddleBlockPosition(int y) =>
        new(GetMiddleBlockX(), y, GetMiddleBlockZ());

    public bool Contains(BlockPos position)
    {
        ArgumentNullException.ThrowIfNull(position);
        return position.GetX() >= GetMinBlockX() &&
               position.GetZ() >= GetMinBlockZ() &&
               position.GetX() <= GetMaxBlockX() &&
               position.GetZ() <= GetMaxBlockZ();
    }

    public BlockPos GetWorldPosition() =>
        new(GetMinBlockX(), 0, GetMinBlockZ());

    public int GetChessboardDistance(ChunkPos position) =>
        GetChessboardDistance(position.X, position.Z);

    public int GetChessboardDistance(int x, int z) =>
        Math.Max(
            JavaAbs(unchecked(x - X)),
            JavaAbs(unchecked(z - Z)));

    public int DistanceSquared(ChunkPos position) =>
        DistanceSquared(position.X, position.Z);

    public int DistanceSquared(long position) =>
        DistanceSquared(GetX(position), GetZ(position));

    private int DistanceSquared(int x, int z)
    {
        int deltaX = unchecked(x - X);
        int deltaZ = unchecked(z - Z);
        return unchecked(deltaX * deltaX + deltaZ * deltaZ);
    }

    /// <summary>
    /// Enumerates an inclusive rectangle. X advances first, matching the reference order.
    /// </summary>
    public static IEnumerable<ChunkPos> RangeClosed(ChunkPos center, int radius) =>
        RangeClosed(
            new ChunkPos(unchecked(center.X - radius), unchecked(center.Z - radius)),
            new ChunkPos(unchecked(center.X + radius), unchecked(center.Z + radius)));

    public static IEnumerable<ChunkPos> RangeClosed(ChunkPos from, ChunkPos to)
    {
        int xDirection = from.X < to.X ? 1 : -1;
        int zDirection = from.Z < to.Z ? 1 : -1;
        int x = from.X;
        int z = from.Z;

        while (true)
        {
            yield return new ChunkPos(x, z);

            if (x == to.X)
            {
                if (z == to.Z)
                    yield break;

                x = from.X;
                z = unchecked(z + zDirection);
            }
            else
            {
                x = unchecked(x + xDirection);
            }
        }
    }

    public override int GetHashCode() => Hash(X, Z);

    public override string ToString() => $"[{X}, {Z}]";

    private static int JavaAbs(int value) =>
        value < 0 ? unchecked(-value) : value;
}

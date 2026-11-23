using Asteria.World.LevelGen.Structure;

namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// A regularly stepped block-coordinate volume used by density-function and noise sampling.
/// Mirrors Minecraft Java 26.3 DensityVolume.
/// </summary>
public sealed record DensityVolume
{
    public const int NO_BLOCK = -1;

    public int SizeX { get; }
    public int SizeY { get; }
    public int SizeZ { get; }
    public int MinBlockX { get; }
    public int MinBlockY { get; }
    public int MinBlockZ { get; }
    public int StepBlockX { get; }
    public int StepBlockY { get; }
    public int StepBlockZ { get; }

    public DensityVolume(
        int sizeX,
        int sizeY,
        int sizeZ,
        int minBlockX,
        int minBlockY,
        int minBlockZ,
        int stepBlockX,
        int stepBlockY,
        int stepBlockZ)
    {
        if (sizeX <= 0 || sizeY <= 0 || sizeZ <= 0)
        {
            throw new ArgumentException(
                $"Size must be positive, was: {sizeX}x{sizeY}x{sizeZ}");
        }

        if (stepBlockX <= 0 || stepBlockY <= 0 || stepBlockZ <= 0)
        {
            throw new ArgumentException(
                $"Step must be positive, was: {stepBlockX}; {stepBlockY}; {stepBlockZ}");
        }

        SizeX = sizeX;
        SizeY = sizeY;
        SizeZ = sizeZ;
        MinBlockX = minBlockX;
        MinBlockY = minBlockY;
        MinBlockZ = minBlockZ;
        StepBlockX = stepBlockX;
        StepBlockY = stepBlockY;
        StepBlockZ = stepBlockZ;
    }

    public DensityVolume(
        int sizeX,
        int sizeY,
        int sizeZ,
        int minBlockX,
        int minBlockY,
        int minBlockZ)
        : this(sizeX, sizeY, sizeZ, minBlockX, minBlockY, minBlockZ, 1, 1, 1)
    {
    }

    public int IndexUnchecked(int indexX, int indexY, int indexZ) =>
        unchecked(indexY + (indexX + indexZ * SizeX) * SizeY);

    public int BlockX(int index) =>
        unchecked(MinBlockX + index * StepBlockX);

    public int BlockY(int index) =>
        unchecked(MinBlockY + index * StepBlockY);

    public int BlockZ(int index) =>
        unchecked(MinBlockZ + index * StepBlockZ);

    public int MaxBlockX() =>
        unchecked(MinBlockX + SizeX * StepBlockX - 1);

    public int MaxBlockY() =>
        unchecked(MinBlockY + SizeY * StepBlockY - 1);

    public int MaxBlockZ() =>
        unchecked(MinBlockZ + SizeZ * StepBlockZ - 1);

    public int Size() =>
        unchecked(SizeX * SizeY * SizeZ);

    public bool Intersects(BoundingBox box)
    {
        ArgumentNullException.ThrowIfNull(box);
        return box.Intersects(
            MinBlockX, MinBlockY, MinBlockZ,
            MaxBlockX(), MaxBlockY(), MaxBlockZ());
    }

    private bool ContainsBlockRelative(int relativeX, int relativeY, int relativeZ) =>
        relativeX >= 0 &&
        relativeY >= 0 &&
        relativeZ >= 0 &&
        relativeX < unchecked(SizeX * StepBlockX) &&
        relativeY < unchecked(SizeY * StepBlockY) &&
        relativeZ < unchecked(SizeZ * StepBlockZ) &&
        FloorMod(relativeX, StepBlockX) == 0 &&
        FloorMod(relativeY, StepBlockY) == 0 &&
        FloorMod(relativeZ, StepBlockZ) == 0;

    public int IndexOfBlock(int blockX, int blockY, int blockZ)
    {
        int relativeX = unchecked(blockX - MinBlockX);
        int relativeY = unchecked(blockY - MinBlockY);
        int relativeZ = unchecked(blockZ - MinBlockZ);

        if (StepBlockX == 1 && StepBlockY == 1 && StepBlockZ == 1)
        {
            if (relativeX >= 0 && relativeY >= 0 && relativeZ >= 0 &&
                relativeX < SizeX && relativeY < SizeY && relativeZ < SizeZ)
            {
                return IndexUnchecked(relativeX, relativeY, relativeZ);
            }
        }
        else if (ContainsBlockRelative(relativeX, relativeY, relativeZ))
        {
            return IndexUnchecked(
                FloorDiv(relativeX, StepBlockX),
                FloorDiv(relativeY, StepBlockY),
                FloorDiv(relativeZ, StepBlockZ));
        }

        return NO_BLOCK;
    }

    private static int FloorDiv(int numerator, int denominator)
    {
        int quotient = numerator / denominator;
        int remainder = numerator % denominator;
        if (remainder != 0 && (numerator ^ denominator) < 0)
            quotient = unchecked(quotient - 1);
        return quotient;
    }

    private static int FloorMod(int numerator, int denominator) =>
        unchecked(numerator - FloorDiv(numerator, denominator) * denominator);
}

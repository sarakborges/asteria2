namespace Asteria.Core;

/// <summary>
/// Converts between block, quart, and section coordinates.
/// </summary>
public static class QuartPos
{
    public const int Bits = 2;
    public const int Size = 4;
    public const int Mask = 3;

    public static int FromBlock(int blockCoordinate) => blockCoordinate >> Bits;

    public static int QuartLocal(int blockCoordinate) => blockCoordinate & Mask;

    public static int ToBlock(int quartCoordinate) => quartCoordinate << Bits;

    public static int FromSection(int sectionCoordinate) => sectionCoordinate << Bits;

    public static int ToSection(int quartCoordinate) => quartCoordinate >> Bits;
}

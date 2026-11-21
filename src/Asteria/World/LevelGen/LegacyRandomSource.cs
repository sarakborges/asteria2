namespace Asteria.World.LevelGen;

/// <summary>
/// 48-bit linear-congruential random source used by legacy world-generation algorithms.
/// </summary>
public sealed class LegacyRandomSource
{
    private const long ModulusMask = 281474976710655L;
    private const long Multiplier = 25214903917L;
    private const long Increment = 11L;
    private long _seed;

    public LegacyRandomSource(long seed) => SetSeed(seed);

    public void SetSeed(long seed) =>
        _seed = (seed ^ Multiplier) & ModulusMask;

    public int NextBits(int bits)
    {
        if (bits is < 1 or > 32)
            throw new ArgumentOutOfRangeException(nameof(bits));

        _seed = unchecked(_seed * Multiplier + Increment) & ModulusMask;
        return (int)(_seed >> (48 - bits));
    }

    public int NextInt() => NextBits(32);

    public int NextInt(int bound)
    {
        if (bound <= 0)
            throw new ArgumentOutOfRangeException(nameof(bound), "bound must be positive");

        if ((bound & -bound) == bound)
            return (int)((bound * (long)NextBits(31)) >> 31);

        int bits;
        int value;
        do
        {
            bits = NextBits(31);
            value = bits % bound;
        }
        while (unchecked(bits - value + (bound - 1)) < 0);

        return value;
    }

    public double NextDouble() =>
        (((long)NextBits(26) << 27) + NextBits(27)) / 9007199254740992.0;
}

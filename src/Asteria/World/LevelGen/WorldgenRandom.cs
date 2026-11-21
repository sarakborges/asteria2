namespace Asteria.World.LevelGen;

/// <summary>
/// World-generation random adapter. It preserves the source's call-count behavior and
/// delegates bit generation to the wrapped legacy source.
/// </summary>
public sealed class WorldgenRandom : IRandomSource
{
    private readonly LegacyRandomSource _randomSource;

    public int Count { get; private set; }

    public WorldgenRandom(LegacyRandomSource randomSource) =>
        _randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));

    public int NextBits(int bits)
    {
        Count++;
        return _randomSource.NextBits(bits);
    }

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

    public float NextFloat() =>
        NextBits(24) * (1.0F / 16777216.0F);

    public double NextDouble() =>
        (((long)NextBits(26) << 27) + NextBits(27)) / 9007199254740992.0;
}

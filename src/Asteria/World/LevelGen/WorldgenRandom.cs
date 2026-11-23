namespace Asteria.World.LevelGen;

/// <summary>Minecraft 26.3 WorldgenRandom: a LegacyRandomSource facade over another RandomSource.</summary>
public sealed class WorldgenRandom : LegacyRandomSource
{
    private readonly IRandomSource? _randomSource;
    public int Count { get; private set; }

    public WorldgenRandom(IRandomSource randomSource) : base(0L)
    {
        _randomSource = randomSource ?? throw new ArgumentNullException(nameof(randomSource));
    }

    public int GetCount() => Count;
    public override IRandomSource Fork() => _randomSource!.Fork();
    public override PositionalRandomFactory ForkPositional() => _randomSource!.ForkPositional();

    public override int NextBits(int bits)
    {
        Count++;
        return _randomSource is LegacyRandomSource legacy
            ? legacy.NextBits(bits)
            : unchecked((int)((ulong)_randomSource!.NextLong() >> (64 - bits)));
    }

    public override void SetSeed(long seed)
    {
        if (_randomSource is not null)
            _randomSource.SetSeed(seed);
    }

    public long SetDecorationSeed(long seed, int chunkX, int chunkZ)
    {
        SetSeed(seed);
        long xScale = NextLong() | 1L;
        long zScale = NextLong() | 1L;
        long result = unchecked(((long)chunkX * xScale + (long)chunkZ * zScale) ^ seed);
        SetSeed(result);
        return result;
    }

    public void SetFeatureSeed(long seed, int index, int step) =>
        SetSeed(unchecked(seed + index + 10000L * step));

    public void SetLargeFeatureSeed(long seed, int chunkX, int chunkZ)
    {
        SetSeed(seed);
        long xScale = NextLong();
        long zScale = NextLong();
        SetSeed(unchecked((long)chunkX * xScale ^ (long)chunkZ * zScale ^ seed));
    }

    public void SetLargeFeatureWithSalt(long seed, int x, int z, int blend) =>
        SetSeed(unchecked((long)x * 341873128712L + (long)z * 132897987541L + seed + blend));
}

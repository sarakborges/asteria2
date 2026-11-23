using System.Text;
using Asteria.Core;

namespace Asteria.World.LevelGen;

/// <summary>Xoroshiro128++ random source and positional factory from Minecraft Java 26.3.</summary>
public sealed class XoroshiroRandomSource : IRandomSource
{
    private const float FloatUnit = 5.9604645E-8F;
    private const float DoubleUnit = 1.110223E-16F;

    private Xoroshiro128PlusPlus _randomNumberGenerator;
    private readonly MarsagliaPolarGaussian _gaussianSource;

    public XoroshiroRandomSource(long seed)
        : this(RandomSupport.UpgradeSeedTo128Bit(seed))
    {
    }

    public XoroshiroRandomSource(RandomSupport.Seed128bit seed)
    {
        _randomNumberGenerator = new Xoroshiro128PlusPlus(seed);
        _gaussianSource = new MarsagliaPolarGaussian(this);
    }

    public XoroshiroRandomSource(long seedLo, long seedHi)
    {
        _randomNumberGenerator = new Xoroshiro128PlusPlus(seedLo, seedHi);
        _gaussianSource = new MarsagliaPolarGaussian(this);
    }

    public IRandomSource Fork() =>
        new XoroshiroRandomSource(_randomNumberGenerator.NextLong(), _randomNumberGenerator.NextLong());

    public PositionalRandomFactory ForkPositional() =>
        new XoroshiroPositionalRandomFactory(
            _randomNumberGenerator.NextLong(),
            _randomNumberGenerator.NextLong());

    public void SetSeed(long seed)
    {
        _randomNumberGenerator = new Xoroshiro128PlusPlus(RandomSupport.UpgradeSeedTo128Bit(seed));
        _gaussianSource.Reset();
    }

    public int NextInt() =>
        unchecked((int)_randomNumberGenerator.NextLong());

    public int NextInt(int bound)
    {
        if (bound <= 0)
            throw new ArgumentException("Bound must be positive", nameof(bound));

        uint randomBits = unchecked((uint)NextInt());
        ulong multipliedRandomBits = (ulong)randomBits * (uint)bound;
        uint fractionalPart = (uint)multipliedRandomBits;

        if (fractionalPart < (uint)bound)
        {
            uint unbiasedBucketsStartIndex = unchecked(0u - (uint)bound) % (uint)bound;
            while (fractionalPart < unbiasedBucketsStartIndex)
            {
                randomBits = unchecked((uint)NextInt());
                multipliedRandomBits = (ulong)randomBits * (uint)bound;
                fractionalPart = (uint)multipliedRandomBits;
            }
        }

        return unchecked((int)(multipliedRandomBits >> 32));
    }

    public long NextLong() =>
        _randomNumberGenerator.NextLong();

    public bool NextBoolean() =>
        (_randomNumberGenerator.NextLong() & 1L) != 0L;

    public float NextFloat() =>
        NextBits(24) * FloatUnit;

    public double NextDouble() =>
        NextBits(53) * DoubleUnit;

    public double NextGaussian() =>
        _gaussianSource.NextGaussian();

    public void ConsumeCount(int rounds)
    {
        for (int i = 0; i < rounds; i++)
            _ = _randomNumberGenerator.NextLong();
    }

    private long NextBits(int bits) =>
        unchecked((long)((ulong)_randomNumberGenerator.NextLong() >> (64 - bits)));

    public sealed class XoroshiroPositionalRandomFactory : PositionalRandomFactory
    {
        private readonly long _seedLo;
        private readonly long _seedHi;

        public XoroshiroPositionalRandomFactory(long seedLo, long seedHi)
        {
            _seedLo = seedLo;
            _seedHi = seedHi;
        }

        public IRandomSource At(int x, int y, int z)
        {
            long positionalSeed = Mth.GetSeed(x, y, z);
            return new XoroshiroRandomSource(positionalSeed ^ _seedLo, _seedHi);
        }

        public IRandomSource FromHashOf(string name)
        {
            RandomSupport.Seed128bit seed = RandomSupport.SeedFromHashOf(name);
            return new XoroshiroRandomSource(seed.Xor(_seedLo, _seedHi));
        }

        public IRandomSource FromSeed(long seed) =>
            new XoroshiroRandomSource(seed ^ _seedLo, seed ^ _seedHi);

        public void ParityConfigString(StringBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.Append("seedLo: ").Append(_seedLo).Append(", seedHi: ").Append(_seedHi);
        }
    }
}

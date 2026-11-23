using System.Text;
using Asteria.Core;

namespace Asteria.World.LevelGen;

/// <summary>48-bit Java LCG random source and positional factory from Minecraft Java 26.3.</summary>
public class LegacyRandomSource : IRandomSource
{
    private const long ModulusMask = 281474976710655L;
    private const long Multiplier = 25214903917L;
    private const long Increment = 11L;
    private long _seed;
    private readonly MarsagliaPolarGaussian _gaussianSource;

    public LegacyRandomSource(long seed)
    {
        _gaussianSource = new MarsagliaPolarGaussian(this);
        SetSeed(seed);
    }

    public virtual void SetSeed(long seed)
    {
        _seed = (seed ^ Multiplier) & ModulusMask;
        _gaussianSource.Reset();
    }

    public virtual int NextBits(int bits)
    {
        if (bits is < 1 or > 32)
            throw new ArgumentOutOfRangeException(nameof(bits));
        _seed = unchecked(_seed * Multiplier + Increment) & ModulusMask;
        return unchecked((int)(_seed >> (48 - bits)));
    }

    public virtual int NextInt() => NextBits(32);

    public virtual int NextInt(int bound)
    {
        if (bound <= 0)
            throw new ArgumentException("Bound must be positive", nameof(bound));

        if ((bound & (bound - 1)) == 0)
            return (int)((bound * (long)NextBits(31)) >> 31);

        int sample, modulo;
        do
        {
            sample = NextBits(31);
            modulo = sample % bound;
        }
        while (unchecked(sample - modulo + (bound - 1)) < 0);

        return modulo;
    }

    public virtual long NextLong()
    {
        int upper = NextBits(32);
        int lower = NextBits(32);
        return unchecked(((long)upper << 32) + lower);
    }

    public virtual bool NextBoolean() => NextBits(1) != 0;
    public virtual float NextFloat() => NextBits(24) * 5.9604645E-8F;

    public virtual double NextDouble()
    {
        int upper = NextBits(26);
        int lower = NextBits(27);
        return (((long)upper << 27) + lower) / 9007199254740992.0;
    }

    public virtual double NextGaussian() => _gaussianSource.NextGaussian();
    public virtual IRandomSource Fork() => new LegacyRandomSource(NextLong());
    public virtual PositionalRandomFactory ForkPositional() => new LegacyPositionalRandomFactory(NextLong());

    public virtual void ConsumeCount(int rounds)
    {
        for (int i = 0; i < rounds; i++)
            _ = NextInt();
    }

    public sealed class LegacyPositionalRandomFactory : PositionalRandomFactory
    {
        private readonly long _seed;
        public LegacyPositionalRandomFactory(long seed) => _seed = seed;

        public IRandomSource At(int x, int y, int z) =>
            new LegacyRandomSource(Asteria.Core.Mth.GetSeed(x, y, z) ^ _seed);

        public IRandomSource FromHashOf(string name)
        {
            ArgumentNullException.ThrowIfNull(name);
            int positionalSeed = 0;
            foreach (char character in name)
                positionalSeed = unchecked(positionalSeed * 31 + character);
            return new LegacyRandomSource(unchecked(positionalSeed ^ _seed));
        }

        public IRandomSource FromSeed(long seed) => new LegacyRandomSource(seed);

        public void ParityConfigString(StringBuilder builder)
        {
            ArgumentNullException.ThrowIfNull(builder);
            builder.Append("LegacyPositionalRandomFactory{").Append(_seed).Append('}');
        }
    }
}

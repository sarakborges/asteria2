using System.Numerics;

namespace Asteria.World.LevelGen;

/// <summary>Xoroshiro128++ state machine from Minecraft Java 26.3.</summary>
public sealed class Xoroshiro128PlusPlus
{
    public long SeedLo { get; private set; }
    public long SeedHi { get; private set; }

    public Xoroshiro128PlusPlus(RandomSupport.Seed128bit seed)
        : this(seed.SeedLo, seed.SeedHi)
    {
    }

    public Xoroshiro128PlusPlus(long seedLo, long seedHi)
    {
        SeedLo = seedLo;
        SeedHi = seedHi;
        if ((SeedLo | SeedHi) == 0L)
        {
            SeedLo = RandomSupport.GOLDEN_RATIO_64;
            SeedHi = RandomSupport.SILVER_RATIO_64;
        }
    }

    public long NextLong()
    {
        long s0 = SeedLo;
        long s1 = SeedHi;
        long result = unchecked(BitOperations.RotateLeft(unchecked(s0 + s1), 17) + s0);
        s1 ^= s0;
        SeedLo = BitOperations.RotateLeft(s0, 49) ^ s1 ^ unchecked(s1 << 21);
        SeedHi = BitOperations.RotateLeft(s1, 28);
        return result;
    }
}

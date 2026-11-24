namespace Asteria.Core;

/// <summary>Linear congruential mixing used by Minecraft Java 26.3 biome zoom.</summary>
public static class LinearCongruentialGenerator
{
    private const long MULTIPLIER = 6364136223846793005L;
    private const long INCREMENT = 1442695040888963407L;

    public static long Next(long value, long salt)
    {
        unchecked
        {
            value *= value * MULTIPLIER + INCREMENT;
            return value + salt;
        }
    }
}

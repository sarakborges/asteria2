using System.Buffers.Binary;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace Asteria.World.LevelGen;

/// <summary>Seed mixing and 128-bit seed operations from Minecraft Java 26.3.</summary>
public static class RandomSupport
{
    public const long GOLDEN_RATIO_64 = -7046029254386353131L;
    public const long SILVER_RATIO_64 = 7640891576956012809L;

    private const long StaffordMultiplier1 = -4658895280553007687L;
    private const long StaffordMultiplier2 = -7723592293110705685L;
    private const long SeedUniquifierMultiplier = 1181783497276652981L;
    private static long _seedUniquifier = 8682522807148012L;

    public static long MixStafford13(long value)
    {
        unchecked
        {
            value = (value ^ (long)((ulong)value >> 30)) * StaffordMultiplier1;
            value = (value ^ (long)((ulong)value >> 27)) * StaffordMultiplier2;
            return value ^ (long)((ulong)value >> 31);
        }
    }

    public static Seed128bit UpgradeSeedTo128BitUnmixed(long legacySeed)
    {
        long lowBits = legacySeed ^ SILVER_RATIO_64;
        long highBits = unchecked(lowBits + GOLDEN_RATIO_64);
        return new Seed128bit(lowBits, highBits);
    }

    public static Seed128bit UpgradeSeedTo128Bit(long legacySeed) =>
        UpgradeSeedTo128BitUnmixed(legacySeed).Mixed();

    public static Seed128bit SeedFromHashOf(string input)
    {
        ArgumentNullException.ThrowIfNull(input);
        byte[] hash = MD5.HashData(Encoding.UTF8.GetBytes(input));
        long low = BinaryPrimitives.ReadInt64BigEndian(hash.AsSpan(0, 8));
        long high = BinaryPrimitives.ReadInt64BigEndian(hash.AsSpan(8, 8));
        return new Seed128bit(low, high);
    }

    public static long GenerateUniqueSeed()
    {
        long current;
        long updated;
        do
        {
            current = Interlocked.Read(ref _seedUniquifier);
            updated = unchecked(current * SeedUniquifierMultiplier);
        }
        while (Interlocked.CompareExchange(ref _seedUniquifier, updated, current) != current);

        long nanoTime = (long)(Stopwatch.GetTimestamp() * (1_000_000_000.0 / Stopwatch.Frequency));
        return updated ^ nanoTime;
    }

    public readonly record struct Seed128bit(long SeedLo, long SeedHi)
    {
        public Seed128bit Xor(long low, long high) =>
            new(SeedLo ^ low, SeedHi ^ high);

        public Seed128bit Xor(Seed128bit other) =>
            Xor(other.SeedLo, other.SeedHi);

        public Seed128bit Mixed() =>
            new(MixStafford13(SeedLo), MixStafford13(SeedHi));
    }
}

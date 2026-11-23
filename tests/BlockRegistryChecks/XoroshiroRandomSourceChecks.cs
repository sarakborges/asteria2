using System.Text;
using Asteria.World.LevelGen;

internal static class XoroshiroRandomSourceChecks
{
    public static void Run()
    {
        CheckSeedSupport();
        CheckXoroshiroStream();
        CheckPositionalFactory();
        CheckWorldgenWrapper();
        CheckGaussianCache();
    }

    private static void CheckSeedSupport()
    {
        RandomSupport.Seed128bit upgraded = RandomSupport.UpgradeSeedTo128Bit(1234L);
        Require(upgraded.SeedLo == -8130857938402457085L &&
            upgraded.SeedHi == -4079312777098371923L,
            "RandomSupport.upgradeSeedTo128bit must preserve Stafford13 seed expansion.");

        RandomSupport.Seed128bit hash = RandomSupport.SeedFromHashOf("octave_-4");
        Require(hash.SeedLo == -4787091969620723870L &&
            hash.SeedHi == -4579290196374760307L,
            "RandomSupport.seedFromHashOf must use MD5 over UTF-8 and big-endian 64-bit words.");

        RandomSupport.Seed128bit xored = hash.Xor(123L, -456L);
        Require(xored.SeedLo == (hash.SeedLo ^ 123L) &&
            xored.SeedHi == (hash.SeedHi ^ -456L) &&
            xored.Mixed() == new RandomSupport.Seed128bit(
                RandomSupport.MixStafford13(xored.SeedLo),
                RandomSupport.MixStafford13(xored.SeedHi)),
            "Seed128bit.xor/mixed must apply each operation to the corresponding 64-bit word.");

        Xoroshiro128PlusPlus zero = new(0L, 0L);
        Xoroshiro128PlusPlus replacement = new(
            RandomSupport.GOLDEN_RATIO_64,
            RandomSupport.SILVER_RATIO_64);
        Require(zero.NextLong() == replacement.NextLong(),
            "Xoroshiro128PlusPlus must replace the all-zero state with the golden/silver seed pair.");
    }

    private static void CheckXoroshiroStream()
    {
        var random = new XoroshiroRandomSource(1234L);
        long[] expected =
        {
            1442210903926503196L,
            -6213764276566792285L,
            -6513712564911937959L,
            5647029953998151295L,
            -7532466972812487698L
        };
        foreach (long value in expected)
            Require(random.NextLong() == value,
                "XoroshiroRandomSource must reproduce the Minecraft 26.3 Xoroshiro128++ stream.");

        random.SetSeed(1234L);
        int[] expectedBounded = { 0, 16, 15, 15, 7 };
        foreach (int value in expectedBounded)
            Require(random.NextInt(17) == value,
                "XoroshiroRandomSource.nextInt(bound) must use unsigned multiply-high rejection sampling.");

        random.SetSeed(1234L);
        var second = new XoroshiroRandomSource(1234L);
        for (int i = 0; i < 16; i++)
            Require(random.NextInt() == second.NextInt(),
                "XoroshiroRandomSource.nextInt must return the low 32 bits of each nextLong.");

        RequireThrows<ArgumentException>(() => new XoroshiroRandomSource(1L).NextInt(0),
            "XoroshiroRandomSource.nextInt(bound) must reject non-positive bounds.");
    }

    private static void CheckPositionalFactory()
    {
        var source = new XoroshiroRandomSource(1234L);
        PositionalRandomFactory factory = source.ForkPositional();
        Require(factory.At(4, -20, 11).NextLong() == 8887137832797772698L,
            "Xoroshiro positional factories must combine Mth.getSeed with seedLo and retain seedHi.");

        Require(factory.FromHashOf("ore_vein_large").NextLong() == 723220374801818644L,
            "Xoroshiro positional factories must hash named streams with RandomSupport.seedFromHashOf.");

        Require(factory.FromSeed(42L).NextLong() == 2809627244593115846L,
            "Xoroshiro positional factory.fromSeed must XOR the supplied seed with both stored seed words.");

        var parity = new StringBuilder();
        factory.ParityConfigString(parity);
        Require(parity.ToString() == "seedLo: 1442210903926503196, seedHi: -6213764276566792285",
            "Xoroshiro positional factory parity text must preserve its two seed words.");
    }

    private static void CheckWorldgenWrapper()
    {
        var source = new XoroshiroRandomSource(1234L);
        var worldgen = new WorldgenRandom(source);
        long nextLong = new XoroshiroRandomSource(1234L).NextLong();
        Require(worldgen.NextInt() == unchecked((int)(uint)((ulong)nextLong >> 32)) &&
            worldgen.GetCount() == 1,
            "WorldgenRandom must adapt Xoroshiro raw bits from the high end of nextLong and increment count once.");

        IRandomSource legacy = WorldgenRandom.Algorithm.LEGACY.NewInstance(99L);
        IRandomSource xoroshiro = WorldgenRandom.Algorithm.XOROSHIRO.NewInstance(99L);
        Require(legacy is LegacyRandomSource && xoroshiro is XoroshiroRandomSource,
            "WorldgenRandom.Algorithm must construct the corresponding vanilla random-source family.");
    }

    private static void CheckGaussianCache()
    {
        var random = new XoroshiroRandomSource(4321L);
        double first = random.NextGaussian();
        double second = random.NextGaussian();
        random.SetSeed(4321L);
        Require(random.NextGaussian() == first && random.NextGaussian() == second,
            "XoroshiroRandomSource.setSeed must reset its cached Marsaglia polar result.");
    }

    private static void RequireThrows<TException>(Action action, string message)
        where TException : Exception
    {
        try { action(); }
        catch (TException) { return; }
        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

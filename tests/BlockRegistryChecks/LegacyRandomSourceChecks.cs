using System.Text;
using Asteria.World.LevelGen;

internal static class LegacyRandomSourceChecks
{
    public static void Run()
    {
        CheckJavaLcgSequence();
        CheckPositionalFactory();
        CheckWorldgenRandom();
        CheckGaussianCache();
    }

    private static void CheckJavaLcgSequence()
    {
        var random = new LegacyRandomSource(1234L);
        int[] expectedInts = { -1517918040, 1115789266, -208917030, 1019800440, -611652875 };
        foreach (int expected in expectedInts)
            Require(random.NextInt() == expected,
                "LegacyRandomSource must match the Java 48-bit LCG nextInt sequence.");

        random.SetSeed(1234L);
        long[] expectedLongs =
        {
            -6519408338692630574L,
            -897291810407650440L,
            -2627029093267243214L,
            8452912497529882771L
        };
        foreach (long expected in expectedLongs)
            Require(random.NextLong() == expected,
                "LegacyRandomSource.nextLong must compose signed upper/lower 32-bit draws as Java does.");

        foreach (int bound in new[] { 1, 2, 16, 17, 31, 256, 1000 })
        {
            var first = new LegacyRandomSource(1234L);
            var second = new LegacyRandomSource(1234L);
            for (int draw = 0; draw < 32; draw++)
            {
                int actual = first.NextInt(bound);
                Require(actual >= 0 && actual < bound && actual == second.NextInt(bound),
                    "LegacyRandomSource.nextInt(bound) must be bounded and deterministic for power-of-two and rejection-sampled ranges.");
            }
        }

        RequireThrows<ArgumentException>(() => new LegacyRandomSource(1L).NextInt(0),
            "LegacyRandomSource.nextInt(bound) must reject non-positive bounds.");
        RequireThrows<ArgumentException>(() => new LegacyRandomSource(1L).NextInt(10, 10),
            "RandomSource.nextInt(origin,bound) must reject a non-positive range.");
    }

    private static void CheckPositionalFactory()
    {
        var factory = new LegacyRandomSource.LegacyPositionalRandomFactory(987654321L);
        long seed = Asteria.Core.Mth.GetSeed(-12, 80, 37) ^ 987654321L;
        Require(factory.At(-12, 80, 37).NextLong() == new LegacyRandomSource(seed).NextLong(),
            "Legacy positional factory must derive coordinate streams with Mth.getSeed XOR factory seed.");

        int nameHash = JavaHash("ore_vein_large");
        Require(factory.FromHashOf("ore_vein_large").NextLong() ==
                new LegacyRandomSource(unchecked(nameHash ^ 987654321L)).NextLong(),
            "Legacy positional factory must use Java String.hashCode for named streams.");
        Require(factory.FromSeed(42L).NextLong() == new LegacyRandomSource(42L).NextLong(),
            "Legacy positional factory.fromSeed must return an independently seeded legacy source.");

        var output = new StringBuilder();
        factory.ParityConfigString(output);
        Require(output.ToString() == "LegacyPositionalRandomFactory{987654321}",
            "Legacy positional factory parity text must match the reference.");
    }

    private static void CheckWorldgenRandom()
    {
        var source = new LegacyRandomSource(1234L);
        var random = new WorldgenRandom(source);
        Require(random.NextInt() == -1517918040 && random.GetCount() == 1,
            "WorldgenRandom.next(bits) must delegate legacy bits and count each raw request.");
        _ = random.NextDouble();
        Require(random.GetCount() == 3,
            "WorldgenRandom.nextDouble must count the two raw bit requests.");
        random.SetSeed(1234L);
        Require(random.GetCount() == 3 &&
                random.NextInt() == new LegacyRandomSource(1234L).NextInt(),
            "WorldgenRandom.setSeed must delegate without resetting its call counter.");

        random.SetFeatureSeed(100L, 2, 3);
        Require(random.NextInt() == new LegacyRandomSource(30102L).NextInt(),
            "WorldgenRandom.setFeatureSeed must use seed + index + 10000 * step.");

        var wrappedFork = new WorldgenRandom(new LegacyRandomSource(4321L)).Fork();
        var expectedFork = new LegacyRandomSource(4321L).Fork();
        Require(wrappedFork.NextLong() == expectedFork.NextLong(),
            "WorldgenRandom.fork must delegate to the selected random source.");

        var wrappedFactory = new WorldgenRandom(new LegacyRandomSource(7654L)).ForkPositional();
        var expectedFactory = new LegacyRandomSource(7654L).ForkPositional();
        Require(wrappedFactory.At(1, 2, 3).NextLong() == expectedFactory.At(1, 2, 3).NextLong(),
            "WorldgenRandom.forkPositional must delegate to the selected random source.");
    }

    private static void CheckGaussianCache()
    {
        var random = new LegacyRandomSource(4321L);
        double first = random.NextGaussian();
        double second = random.NextGaussian();
        random.SetSeed(4321L);
        Require(random.NextGaussian() == first && random.NextGaussian() == second,
            "LegacyRandomSource.setSeed must reset the Marsaglia polar cached result.");
    }

    private static int JavaHash(string value)
    {
        int hash = 0;
        foreach (char character in value)
            hash = unchecked(hash * 31 + character);
        return hash;
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

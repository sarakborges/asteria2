using Asteria.Content.Identifiers;
using Asteria.Content.Biomes;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.Synth;

internal static class RandomStateChecks
{
    private sealed class NoiseFixture
    {
        public Registry<NormalNoise> Registry { get; } =
            new(Registries.NOISE.Location);

        public Holder<NormalNoise>.Reference Temperature { get; }
        public Holder<NormalNoise>.Reference NetherTemperature { get; }
        public Holder<NormalNoise>.Reference NetherVegetation { get; }
        public NormalNoise NormalNoise { get; }
        public NormalNoise NetherTemperatureNoise { get; }
        public NormalNoise NetherVegetationNoise { get; }

        public NoiseFixture()
        {
            NormalNoise = NormalNoise.CreateBuilder()
                .SetBaseOctave(-2)
                .SetOctaveCount(4)
                .Build();
            NetherTemperatureNoise = NormalNoise.CreateBuilder()
                .SetBaseOctave(-3)
                .SetOctaveCount(4)
                .Build();
            NetherVegetationNoise = NormalNoise.CreateBuilder()
                .SetBaseOctave(-4)
                .SetOctaveCount(4)
                .Build();

            Temperature = Register(Noises.TEMPERATURE, NormalNoise);
            NetherTemperature = Register(Noises.TEMPERATURE_NETHER, NetherTemperatureNoise);
            NetherVegetation = Register(Noises.VEGETATION_NETHER, NetherVegetationNoise);
            Registry.Freeze();
        }

        private Holder<NormalNoise>.Reference Register(
            RegistryKey<NormalNoise> key,
            NormalNoise value) =>
            Registry.Register(key, value, RegistrationInfo.BuiltIn);
    }

    public static void Run()
    {
        CheckNoiseKeys();
        CheckNoiseInstanceCache();
        CheckDensityBufferPoolLifecycle();
        CheckRandomFactoriesAndSeeds();
        CheckDensityCompilationAndClimateSamplers();
        CheckSpecialNetherNoisePaths();
    }

    private static NoiseRouter CreateRouter(
        Holder<NormalNoise> noise,
        NormalNoise normalNoise) =>
        new(
            new NoiseFunction(noise, 0.125, 0.25,
                DensityFunctions.Zero(), DensityFunctions.Zero(), DensityFunctions.Zero()),
            new ConstantFunction(0.1f),
            new ConstantFunction(0.2f),
            new ConstantFunction(0.3f),
            new ConstantFunction(0.4f),
            new ConstantFunction(0.5f),
            new BlendedNoise(0.25, 0.125, 80.0, 160.0, 8.0),
            new ConstantFunction(-0.75f));

    private static void CheckNoiseKeys()
    {
        Require(
            Noises.TEMPERATURE.Location == ResourceLocation.Parse("minecraft:temperature") &&
            Noises.SHIFT.Location == ResourceLocation.Parse("minecraft:offset") &&
            Noises.TEMPERATURE_NETHER.Location == ResourceLocation.Parse("minecraft:nether/temperature") &&
            Noises.VEGETATION_NETHER.Location == ResourceLocation.Parse("minecraft:nether/vegetation") &&
            Noises.SWAMP.Location == ResourceLocation.Parse("minecraft:surface_swamp"),
            "Noises must expose the canonical Minecraft 26.3 normal-noise keys for the Asteria worldgen registry.");

        Require(
            Noises.TEMPERATURE.RegistryLocation == Registries.NOISE.Location &&
            Noises.NETHER_STATE_SELECTOR.RegistryLocation == Registries.NOISE.Location,
            "Every Noises key must belong to the worldgen/noise registry.");
    }

    private static void CheckNoiseInstanceCache()
    {
        var fixture = new NoiseFixture();
        var router = CreateRouter(fixture.Temperature, fixture.NormalNoise);
        var state = new RandomState(fixture.Registry, 0x1234_5678_9ABC_DEF0L, false, router);

        Noise first = state.GetOrCreateNoise(Noises.TEMPERATURE);
        Noise second = state.GetOrCreateNoise(Noises.TEMPERATURE);
        Require(
            ReferenceEquals(first, second),
            "RandomState must memoize normal-noise instances by registry key.");

        NoiseFunction function = new(
            fixture.Temperature,
            0.75,
            1.25,
            DensityFunctions.Zero(),
            DensityFunctions.Zero(),
            DensityFunctions.Zero());
        Noise samplerNoise = state.CreateNoiseSampler(fixture.Temperature);
        Require(
            ReferenceEquals(samplerNoise, state.GetOrCreateNoise(Noises.TEMPERATURE)),
            "Density compilation must resolve registered NormalNoise holders through RandomState's shared cache.");

        float value = state.GetSampler(function)
            .SampleValue(SamplerContext.EmptyUncached, -17, 42, 91);
        float repeated = state.GetSampler(function)
            .SampleValue(SamplerContext.EmptyUncached, -17, 42, 91);

        Require(
            float.IsFinite(value) && value == repeated,
            "RandomState density samplers must be memoized and deterministic for fixed inputs.");

        Require(
            ReferenceEquals(state.GetSampler(function), state.GetSampler(function)),
            "RandomState must return the same compiled sampler for equal density-function graph keys.");
    }

    private static void CheckDensityBufferPoolLifecycle()
    {
        var fixture = new NoiseFixture();
        NoiseRouter router = CreateRouter(fixture.Temperature, fixture.NormalNoise);
        var state = new RandomState(fixture.Registry, 0x6A09_E667_F3BC_C909L, false, router);

        DensityBufferPool pool = state.AcquireDensityBufferPool();
        ScopedDensityBuffer buffer = pool.Acquire(1);
        Require(
            buffer.Capacity() == 16 && buffer.Size() == 1,
            "RandomState must provide a pool using the vanilla density-buffer capacity policy.");

        buffer.Close();
        state.ReleaseDensityBufferPool(pool);
        Require(
            ReferenceEquals(state.AcquireDensityBufferPool(), pool),
            "RandomState must reuse the most recently released density-buffer pool.");

        ScopedDensityBuffer reused = pool.Acquire(7);
        Require(
            ReferenceEquals(reused, buffer) && reused.Size() == 7,
            "A pool reacquired from RandomState must retain its reusable density buffers.");
        reused.Close();
        state.ReleaseDensityBufferPool(pool);

        for (int age = 1; age <= 20; age++)
        {
            state.GarbageCollect();
            Require(
                !pool.IsEmpty() && reused.Age == age,
                "RandomState garbage collection must retain pooled buffers through the twentieth idle age.");
        }

        state.GarbageCollect();
        Require(
            pool.IsEmpty() && reused.Age == 21,
            "RandomState garbage collection must discard buffers after their age exceeds twenty.");
        DensityBufferPool replacement = state.AcquireDensityBufferPool();
        Require(
            !ReferenceEquals(replacement, pool),
            "RandomState must stop reusing a pool after garbage collection empties and removes it.");

        var acquired = new List<DensityBufferPool>();
        for (int index = 0; index < 17; index++)
        {
            DensityBufferPool candidate = state.AcquireDensityBufferPool();
            ScopedDensityBuffer candidateBuffer = candidate.Acquire(1);
            candidateBuffer.Close();
            acquired.Add(candidate);
        }

        foreach (DensityBufferPool candidate in acquired)
            state.ReleaseDensityBufferPool(candidate);

        var retained = acquired.Take(16).ToHashSet();
        for (int index = 0; index < 16; index++)
        {
            DensityBufferPool candidate = state.AcquireDensityBufferPool();
            Require(
                retained.Contains(candidate),
                "RandomState must retain no more than sixteen pools and reuse every retained pool.");
        }

        DensityBufferPool overflow = state.AcquireDensityBufferPool();
        Require(
            !retained.Contains(overflow) && !ReferenceEquals(overflow, acquired[16]),
            "RandomState must discard a released pool when its sixteen-pool retention limit is reached.");
    }

    private static void CheckRandomFactoriesAndSeeds()
    {
        const long seed = 0x5A17_2026_7ABC_DEF0L;
        var fixture = new NoiseFixture();
        NoiseRouter router = CreateRouter(fixture.Temperature, fixture.NormalNoise);
        var modern = new RandomState(fixture.Registry, seed, false, router);
        var legacy = new RandomState(fixture.Registry, seed, true, router);

        ResourceLocation factoryName = ResourceLocation.Parse("asteria:test/factory");
        PositionalRandomFactory factoryA = modern.GetOrCreateRandomFactory(factoryName);
        PositionalRandomFactory factoryB = modern.GetOrCreateRandomFactory(factoryName);
        Require(
            ReferenceEquals(factoryA, factoryB),
            "RandomState must memoize positional random factories by identifier.");

        Require(
            factoryA.At(-3, 7, 11).NextLong() ==
            factoryB.At(-3, 7, 11).NextLong(),
            "Positional random factories returned by RandomState must be deterministic by coordinates.");

        IRandomSource legacyTerrainRandom = legacy.CreateRandom(BlendedNoise.NOISE_SEED);
        IRandomSource expectedLegacyTerrainRandom = new LegacyRandomSource(seed);
        Require(
            legacyTerrainRandom.NextLong() == expectedLegacyTerrainRandom.NextLong() &&
            legacyTerrainRandom.NextLong() == expectedLegacyTerrainRandom.NextLong(),
            "Legacy RandomState must use a LegacyRandomSource seeded directly for the vanilla terrain noise seed.");

        IRandomSource modernTerrainRandom = modern.CreateRandom(BlendedNoise.NOISE_SEED);
        IRandomSource expectedModernTerrainRandom =
            new XoroshiroRandomSource(seed).ForkPositional()
                .FromHashOf(BlendedNoise.NOISE_SEED);

        Require(
            modernTerrainRandom.NextLong() == expectedModernTerrainRandom.NextLong(),
            "Modern RandomState must derive terrain noise random streams from the positional random factory.");

        IRandomSource endIslandRandom = modern.CreateEndIslandRandom();
        IRandomSource expectedEndIslandRandom = new LegacyRandomSource(seed);
        Require(
            endIslandRandom.NextLong() == expectedEndIslandRandom.NextLong(),
            "RandomState end-island random must use LegacyRandomSource with the world seed.");
    }

    private static void CheckDensityCompilationAndClimateSamplers()
    {
        var fixture = new NoiseFixture();
        NoiseRouter router = CreateRouter(fixture.Temperature, fixture.NormalNoise);
        var state = new RandomState(fixture.Registry, 0x1020_3040_5060_7080L, false, router);

        DensitySamplerSet samplers = state.SamplersWithContext(SamplerContext.EmptyUncached);
        Climate.Sampler climateSampler = router.CreateClimateSampler(samplers);
        Climate.TargetPoint target = climateSampler.Sample(2, -3, 4);

        float temperature = state.SampleBlockValueUncached(router.Temperature, 8, -12, 16);
        float vegetation = state.SampleBlockValueUncached(router.Vegetation, 8, -12, 16);
        float continents = state.SampleBlockValueUncached(router.Continents, 8, -12, 16);
        float erosion = state.SampleBlockValueUncached(router.Erosion, 8, -12, 16);
        float depth = state.SampleBlockValueUncached(router.Depth, 8, -12, 16);
        float ridges = state.SampleBlockValueUncached(router.Ridges, 8, -12, 16);

        Require(
            target == Climate.Target(
                temperature,
                vegetation,
                continents,
                erosion,
                depth,
                ridges),
            "RandomState climate sampling must bind the router's six climate channels at quart-to-block coordinates.");

        Require(
            float.IsFinite(state.SampleBlockValueUncached(router.FinalDensity, 8, -12, 16)),
            "RandomState must compile and evaluate terrain density functions independently of climate sampling.");
    }

    private static void CheckSpecialNetherNoisePaths()
    {
        const long seed = 0x1234_5678L;
        var fixture = new NoiseFixture();
        NoiseRouter router = CreateRouter(fixture.Temperature, fixture.NormalNoise);
        var state = new RandomState(fixture.Registry, seed, false, router);

#pragma warning disable CS0618
        Noise expectedTemperature = fixture.NetherTemperatureNoise.CreateForLegacyNetherBiome(
            new LegacyRandomSource(seed));
        Noise expectedVegetation = fixture.NetherVegetationNoise.CreateForLegacyNetherBiome(
            new LegacyRandomSource(unchecked(seed + 1L)));
#pragma warning restore CS0618

        Noise actualTemperature = state.CreateNoiseSampler(fixture.NetherTemperature);
        Noise actualVegetation = state.CreateNoiseSampler(fixture.NetherVegetation);

        Require(
            !ReferenceEquals(actualTemperature, state.GetOrCreateNoise(Noises.TEMPERATURE_NETHER)) &&
            !ReferenceEquals(actualVegetation, state.GetOrCreateNoise(Noises.VEGETATION_NETHER)),
            "Nether temperature and vegetation holders must use the special legacy noise construction instead of the normal registered-noise cache.");

        Require(
            Sample(actualTemperature, 17, 64, -29) ==
            Sample(expectedTemperature, 17, 64, -29) &&
            Sample(actualVegetation, 17, 64, -29) ==
            Sample(expectedVegetation, 17, 64, -29),
            "Nether temperature and vegetation noise must use legacy random seeds worldSeed and worldSeed+1.");

        bool directHolderRejected = false;
        try
        {
            _ = state.CreateNoiseSampler(Holder<NormalNoise>.Direct(fixture.NormalNoise));
        }
        catch (InvalidOperationException exception) when (
            exception.Message.Contains("has no registered key", StringComparison.Ordinal))
        {
            directHolderRejected = true;
        }

        Require(
            directHolderRejected,
            "RandomState normal-noise compilation must require registry-backed holders for keyed noise instances.");
    }

    private static float Sample(Noise noise, int x, int y, int z) =>
        noise.Get(x * 0.25, y * 0.25, z * 0.25);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

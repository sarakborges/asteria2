using Asteria.Content.Blocks;
using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.DensityFunctions.Operations;
using Asteria.World.LevelGen.Materials.Rules;
using DensityFunctionFactories = Asteria.World.LevelGen.DensityFunctions.DensityFunctions;

internal static class NoiseChunkChecks
{
    private sealed class TestBlender(float alpha, float offset) : IChunkBlender
    {
        public bool IsEmpty => false;

        public float BlendDensity(int blockX, int blockY, int blockZ, float density) =>
            density + 2.0f;

        public BlenderOutputBuffer BlendOffsetAndFactor(DensityVolume volume)
        {
            int minCellX = QuartPos.FromBlock(volume.MinBlockX);
            int minCellZ = QuartPos.FromBlock(volume.MinBlockZ);
            int cellCountX = QuartPos.FromBlock(volume.MaxBlockX()) - minCellX + 1;
            int cellCountZ = QuartPos.FromBlock(volume.MaxBlockZ()) - minCellZ + 1;
            int count = checked(cellCountX * cellCountZ);
            var alphas = new float[count];
            var offsets = new float[count];
            Array.Fill(alphas, alpha);
            Array.Fill(offsets, offset);
            return new BlenderOutputBuffer(
                minCellX, minCellZ, cellCountX, cellCountZ, alphas, offsets);
        }
    }

    public static void Run()
    {
        CheckNeutralChunkContext();
        CheckBlenderContextAndSampling();
        CheckAquiferSelection();
        CheckContextAndBufferPoolLifecycle();
    }

    private static void CheckNeutralChunkContext()
    {
        NoiseGeneratorSettings settings = CreateSettings(
            new ConstantFunction(0.5f),
            aquifers: null);
        RandomState randomState = CreateRandomState(settings);
        DensityVolume volume = CreateVolume();

        using var chunk = new NoiseChunk(
            randomState,
            beardifier: null,
            settings,
            GlobalWater,
            blender: null,
            volume);

        Require(chunk.Volume == volume,
            "NoiseChunk must retain the exact generation DensityVolume.");
        Require(chunk.Aquifer.ComputeSubstance(0, 9, 0, -1.0) == Blocks.WATER.DefaultState,
            "NoiseChunk must create the disabled aquifer when settings omit aquifers.");
        Require(
            !chunk.Aquifer.ShouldScheduleFluidUpdate(),
            "The disabled aquifer exposed by NoiseChunk must never schedule fluid updates.");

        Require(
            chunk.CachingSamplers.Get(SimpleDensityFunction.BLEND_ALPHA)
                .SampleValue(0, 0, 0) == 1.0f,
            "A chunk without a blender must use the vanilla blend-alpha fallback of one.");
        Require(
            chunk.CachingSamplers.Get(SimpleDensityFunction.BLEND_OFFSET)
                .SampleValue(0, 0, 0) == 0.0f,
            "A chunk without a blender must use the vanilla blend-offset fallback of zero.");
        Require(
            chunk.CachingSamplers.Get(SimpleDensityFunction.BEARDIFIER)
                .SampleValue(0, 0, 0) == 0.0f,
            "A chunk without a Beardifier must bind Beardifier.EMPTY and evaluate it to zero.");
    }

    private static void CheckBlenderContextAndSampling()
    {
        NoiseGeneratorSettings settings = CreateSettings(
            new BlendDensityFunction(new ConstantFunction(0.25f)),
            aquifers: null);
        RandomState randomState = CreateRandomState(settings);
        DensityVolume volume = CreateVolume();
        var blender = new TestBlender(alpha: 0.375f, offset: -2.5f);

        using var chunk = new NoiseChunk(
            randomState,
            beardifier: null,
            settings,
            GlobalWater,
            blender,
            volume);

        Require(
            chunk.CachingSamplers.Get(SimpleDensityFunction.BLEND_ALPHA)
                .SampleValue(4, 12, 8) == 0.375f,
            "NoiseChunk must bind the blender's per-volume blend alpha sampler.");
        Require(
            chunk.CachingSamplers.Get(SimpleDensityFunction.BLEND_OFFSET)
                .SampleValue(4, 12, 8) == -2.5f,
            "NoiseChunk must bind the blender's per-volume blend offset sampler.");
        Require(
            chunk.CachingSamplers.Get(settings.NoiseRouter.FinalDensity)
                .SampleValue(4, 12, 8) == 2.25f,
            "BlendDensityFunction must read the chunk blender from NoiseChunk's sampling context.");
    }

    private static void CheckAquiferSelection()
    {
        NoiseGeneratorSettings settings = CreateSettings(
            new ConstantFunction(-0.25f),
            CreateAquiferConfig());
        RandomState randomState = CreateRandomState(settings);

        using var chunk = new NoiseChunk(
            randomState,
            beardifier: null,
            settings,
            GlobalWater,
            IChunkBlender.Empty,
            CreateVolume());

        Require(
            chunk.Aquifer is NoiseBasedAquifer,
            "NoiseChunk must construct NoiseBasedAquifer when NoiseGeneratorSettings contains aquifer settings.");
    }

    private static void CheckContextAndBufferPoolLifecycle()
    {
        DensityFunction cachedFinalDensity =
            DensityFunctionFactories.Cache(new ConstantFunction(0.75f));
        NoiseGeneratorSettings settings = CreateSettings(cachedFinalDensity, aquifers: null);
        RandomState randomState = CreateRandomState(settings);
        var volume = new DensityVolume(2, 2, 2, 0, 0, 0);
        var chunk = new NoiseChunk(
            randomState,
            beardifier: null,
            settings,
            GlobalWater,
            IChunkBlender.Empty,
            volume);

        DensitySampler.Bound bound = chunk.GetSampler(settings.NoiseRouter.FinalDensity);
        using (ScopedDensityBuffer sampled = bound.SampleVolume(volume))
        {
            Require(
                sampled.Size() == volume.Size() &&
                Enumerable.Range(0, sampled.Size()).All(index => sampled.Get(index) == 0.75f),
                "NoiseChunk's bound cache sampler must sample the full requested volume into its context-owned arena.");
        }

        chunk.Close();
        Require(chunk.IsClosed,
            "NoiseChunk.Close must release its sampler context and buffer-pool lease.");
        chunk.Close();
        Require(chunk.IsClosed,
            "NoiseChunk.Close must be idempotent.");

        Capture<ObjectDisposedException>(
            () => bound.SampleValue(0, 0, 0),
            "A sampler bound to a closed NoiseChunk context must reject further evaluation.");
        Capture<ObjectDisposedException>(
            () => chunk.GetSampler(settings.NoiseRouter.FinalDensity),
            "NoiseChunk.GetSampler must reject requests after its context has been closed.");
    }

    private static NoiseGeneratorSettings CreateSettings(
        DensityFunction finalDensity,
        Aquifer.Config? aquifers)
    {
        var climateDensity = new ConstantFunction(0.0f);
        var router = new NoiseRouter(
            climateDensity,
            climateDensity,
            climateDensity,
            climateDensity,
            climateDensity,
            climateDensity,
            new ConstantFunction(0.0f),
            finalDensity);

        return new NoiseGeneratorSettings(
            NoiseSettings.Create(-64, 384),
            Blocks.STONE.DefaultState,
            Blocks.WATER.DefaultState,
            router,
            Holder<MaterialRule>.Direct(new BlockRule(Blocks.STONE.DefaultState)),
            Array.Empty<SpawnTargetPoint>(),
            63,
            aquifers,
            false,
            NoiseGeneratorDebugFunctions.EMPTY);
    }

    private static RandomState CreateRandomState(NoiseGeneratorSettings settings) =>
        RandomState.Create(
            BuiltInRegistries.Noise,
            918273645L,
            settings.UseLegacyRandomSource,
            settings.NoiseRouter);

    private static DensityVolume CreateVolume() =>
        new(16, 16, 16, 0, -64, 0);

    private static Aquifer.FluidPicker GlobalWater =>
        static (_, _, _) => new Aquifer.FluidStatus(100, Blocks.WATER.DefaultState);

    private static Aquifer.Config CreateAquiferConfig() =>
        new(
            new ConstantFunction(0.1f),
            new ConstantFunction(0.2f),
            new ConstantFunction(0.3f),
            new ConstantFunction(0.4f),
            new ConstantFunction(0.5f),
            new ConstantFunction(0.6f));

    private static TException Capture<TException>(Action action, string message)
        where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException exception)
        {
            return exception;
        }

        throw new InvalidOperationException(message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

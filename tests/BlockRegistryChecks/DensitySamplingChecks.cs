using Asteria.Core;
using Asteria.Util.Context;
using Asteria.World.LevelGen.DensityFunctions;

internal static class DensitySamplingChecks
{
    private sealed class CountingArena : DensityBufferArena
    {
        public int Acquired { get; private set; }
        public int Released { get; private set; }

        public ScopedDensityBuffer Acquire(int size)
        {
            Acquired++;
            return new ScopedDensityBuffer(this, size, size);
        }

        public void Release(ScopedDensityBuffer buffer) => Released++;
    }

    private sealed class CoordinateSampler : DensitySampler
    {
        public int PointSamples { get; private set; }
        public int VolumeSamples { get; private set; }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            PointSamples++;
            return blockX * 10000 + blockY * 100 + blockZ;
        }

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            VolumeSamples++;
            DensitySampler.SampleVolumeNaive(context, outputBuffer, volume, this);
        }
    }

    public static void Run()
    {
        CheckContextMap();
        CheckScopedBuffer();
        CheckNaiveSamplingAndBind();
        CheckSamplerContextCaches();
    }

    private static void CheckContextMap()
    {
        var key = ContextKey<int>.Vanilla("density_test");
        var otherKey = ContextKey<int>.Vanilla("density_test");
        ContextMap empty = ContextMap.Empty;
        Require(!empty.Has(key) && empty.Get(key) == 0 &&
            empty.GetOrDefault(key, 19) == 19,
            "ContextMap.EMPTY must be immutable and return the supplied default for absent parameters.");

        ContextMap map = ContextMap.CreateBuilder()
            .WithParameter(key, 42)
            .Build();
        Require(map.Has(key) && map.Get(key) == 42 &&
            map.GetOrDefault(key, 19) == 42 &&
            !map.Has(otherKey),
            "ContextMap must key parameters by ContextKey object identity and preserve typed values.");
        Require(map.GetOrThrow(key) == 42,
            "ContextMap.getOrThrow must return the present parameter.");
        RequireThrows<KeyNotFoundException>(() => map.GetOrThrow(otherKey),
            "ContextMap.getOrThrow must reject an absent identity-distinct key.");

        ContextMap replaced = ContextMap.CreateBuilder()
            .WithParameter(key, 7)
            .WithOptionalParameter<int>(key, null)
            .Build();
        Require(!replaced.Has(key),
            "ContextMap.withOptionalParameter must remove a parameter when its value is null.");

        var builder = ContextMap.CreateBuilder().WithParameter(key, 8);
        Require(builder.GetParameter(key) == 8 && builder.GetOptionalParameter(key) == 8,
            "ContextMap.Builder must expose the parameter values currently staged.");
    }

    private static void CheckScopedBuffer()
    {
        var arena = new CountingArena();
        var buffer = new ScopedDensityBuffer(arena, capacity: 8, size: 4);
        Require(buffer.Capacity() == 8 && buffer.Size() == 4 &&
            !buffer.IsClosed && buffer.Age == 0,
            "ScopedDensityBuffer must distinguish capacity from its active size.");
        RequireThrows<ArgumentException>(
            () => buffer.Restore(9),
            "ScopedDensityBuffer.restore must reject a requested size larger than capacity.");
        RequireThrows<ArgumentException>(
            () => buffer.Restore(3),
            "ScopedDensityBuffer.restore must reject a buffer that is already in use.");

        buffer.Close();
        buffer.Close();
        Require(buffer.IsClosed && arena.Released == 1,
            "ScopedDensityBuffer.close must release the arena once and remain idempotent.");
        buffer.Restore(3);
        Require(!buffer.IsClosed && buffer.Size() == 3 && buffer.Age == 0,
            "ScopedDensityBuffer.restore must reopen a closed buffer with its requested active size.");
        Require(buffer.IncrementAge() == 1 && buffer.IncrementAge() == 2,
            "ScopedDensityBuffer.incrementAge must increment and return the current age.");
        buffer.Dispose();
        Require(arena.Released == 2 && buffer.IsClosed,
            "IDisposable must delegate to the same scoped-buffer close operation.");
    }

    private static void CheckNaiveSamplingAndBind()
    {
        var sampler = new CoordinateSampler();
        SamplerContext context = SamplerContext.EmptyUncached;
        var volume = new DensityVolume(2, 2, 2, -1, 10, 20);
        DensityBuffer buffer = DensityBuffer.CreateUnpooled(volume.Size());

        DensitySampler.SampleVolumeNaive(context, buffer, volume, sampler);
        float[] expected =
        {
            -8980, -8880,
            1020, 1120,
            -8979, -8879,
            1021, 1121
        };
        for (int i = 0; i < expected.Length; i++)
            Require(buffer.Get(i) == expected[i],
                "DensitySampler.sampleVolumeNaive must traverse in Z/X/Y order and use DensityVolume's Y-minor index layout.");

        DensitySampler.Bound bound = sampler.Bind(context);
        Require(bound.SampleValue(-1, 11, 20) == -8880,
            "DensitySampler.bind must retain and forward the supplied SamplerContext.");
        var boundBuffer = bound.SampleVolume(volume);
        Require(boundBuffer.Size() == volume.Size() && boundBuffer.Get(0) == -8980,
            "DensitySampler.Bound.sampleVolume(volume) must acquire a scoped buffer and sample into it.");
        boundBuffer.Close();
        Require(sampler.VolumeSamples == 1,
            "DensitySampler.Bound.sampleVolume(buffer, volume) must delegate exactly once to the underlying sampler.");
    }

    private static void CheckSamplerContextCaches()
    {
        var arena = new CountingArena();
        SamplerContext context = SamplerContext.CreateBuilder()
            .UseBufferArena(arena)
            .EnableCaches()
            .Build();
        var sampler = new CoordinateSampler();
        var volume = new DensityVolume(2, 2, 2, 0, 5, 0);

        DensityBuffer first = DensityBuffer.CreateUnpooled(volume.Size());
        context.SampleVolumeCached(3, sampler, first, volume);
        Require(sampler.VolumeSamples == 1 && sampler.PointSamples == 8 &&
            arena.Acquired == 1,
            "SamplerContext.sampleVolumeCached must materialize one scoped volume on the first request.");

        DensityBuffer second = DensityBuffer.CreateUnpooled(volume.Size());
        context.SampleVolumeCached(3, sampler, second, volume);
        Require(sampler.VolumeSamples == 1 && arena.Acquired == 1 &&
            Enumerable.Range(0, volume.Size()).All(i => first.Get(i) == second.Get(i)),
            "SamplerContext.sampleVolumeCached must reuse a cached volume when the DensityVolume matches.");

        float bufferedValue = context.SampleValueCached(3, sampler, 1, 6, 1);
        Require(bufferedValue == 10601.0f && sampler.PointSamples == 8,
            "SamplerContext.sampleValueCached must read point samples directly from a matching cached volume.");

        float pointValue = context.SampleValueCached(4, sampler, 50, 60, 70);
        int pointCount = sampler.PointSamples;
        Require(context.SampleValueCached(4, sampler, 50, 60, 70) == pointValue &&
            sampler.PointSamples == pointCount,
            "SamplerContext.sampleValueCached must reuse a matching non-NaN point cache entry.");

        var differentVolume = new DensityVolume(2, 2, 2, 20, 5, 0);
        context.SampleVolumeCached(3, sampler, second, differentVolume);
        Require(arena.Acquired == 2 && arena.Released == 1 && sampler.VolumeSamples == 2,
            "SamplerContext.sampleVolumeCached must close and replace a cached buffer when the volume changes.");

        context.SampleValueCached(4, sampler, 51, 60, 70);
        Require(sampler.PointSamples == pointCount + 1,
            "Point cache entries must be invalidated by a different coordinate.");
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

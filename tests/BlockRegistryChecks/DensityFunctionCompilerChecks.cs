using System.Collections.Concurrent;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Util;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;

internal static class DensityFunctionCompilerChecks
{
    private sealed class Counters
    {
        public int CompileCalls;
        public int PointCalls;
        public int VolumeCalls;
    }

    private sealed record CountingFunction(
        Counters Counters,
        float Value,
        int Axes = DensityFunction.ALL_AXES) : DensityFunction
    {
        public DensitySampler CompileSampler(DensityFunction.CompileContext context)
        {
            Interlocked.Increment(ref Counters.CompileCalls);
            return new CountingSampler(Counters, Value);
        }

        public DensityFunction RewriteChildren(DfRewriteRule rule) =>
            this;

        public Interval Range() =>
            Interval.OfExact(Value);

        public int DomainAxes() =>
            Axes;
    }

    private sealed record CountingSampler(Counters Counters, float Value) : DensitySampler
    {
        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            Interlocked.Increment(ref Counters.PointCalls);
            return Value + blockX * 1000.0f + blockY * 10.0f + blockZ;
        }

        public void SampleVolume(
            SamplerContext context,
            DensityBuffer outputBuffer,
            DensityVolume volume)
        {
            Interlocked.Increment(ref Counters.VolumeCalls);
            DensitySampler.SampleVolumeNaive(context, outputBuffer, volume, this);
        }
    }

    private sealed class CountingArena : DensityBufferArena
    {
        public int Acquired { get; private set; }
        public int Released { get; private set; }

        public ScopedDensityBuffer Acquire(int size)
        {
            Acquired++;
            return new ScopedDensityBuffer(this, size, size);
        }

        public void Release(ScopedDensityBuffer buffer) =>
            Released++;
    }

    private sealed class UnusedCompileContext : DensityFunction.CompileContext
    {
        public Asteria.World.LevelGen.Synth.Noise CreateNoiseSampler(
            Holder<Asteria.World.LevelGen.Synth.NormalNoise> parameters) =>
            throw new InvalidOperationException("The counting fixture must not request noise.");

        public IRandomSource CreateRandom(ResourceLocation seed) =>
            throw new InvalidOperationException("The counting fixture must not request random streams.");

#pragma warning disable CS0618
        public IRandomSource CreateEndIslandRandom() =>
            throw new InvalidOperationException("The counting fixture must not request End Island random streams.");
#pragma warning restore CS0618
    }

    public static void Run()
    {
        CheckMemoizedCompilationIsThreadSafe();
        CheckHolderInliningBeforeCompilation();
        CheckPointCachingAndUncachedContext();
        CheckVolumeCachingAndReplacement();
        CheckEqualCacheInputsReuseOnePreparedCache();
        CheckNestedCachesReceiveDistinctIds();
    }

    private static void CheckMemoizedCompilationIsThreadSafe()
    {
        var counters = new Counters();
        DensityFunction function = new CountingFunction(counters, 3.5f);
        var compiler = new DensityFunctionCompiler(new UnusedCompileContext());
        var samplers = new ConcurrentBag<DensitySampler>();

        Parallel.For(0, 64, _ => samplers.Add(compiler.GetSampler(function)));

        DensitySampler first = samplers.First();
        Require(
            counters.CompileCalls == 1 &&
            samplers.All(sampler => ReferenceEquals(sampler, first)),
            "DensityFunctionCompiler must compile and publish one sampler for concurrent requests of an equal graph.");

        Require(
            first.SampleValue(SamplerContext.EmptyUncached, 1, 2, 3) == 1025.5f,
            "The memoized sampler must preserve the input's point evaluation.");
    }

    private static void CheckHolderInliningBeforeCompilation()
    {
        var counters = new Counters();
        DensityFunction input = new CountingFunction(counters, 7.0f);
        var holder = new DensityFunctions.HolderHolder(Holder<DensityFunction>.Direct(input));
        var compiler = new DensityFunctionCompiler(new UnusedCompileContext());

        DensitySampler sampler = compiler.GetSampler(holder);
        Require(
            counters.CompileCalls == 1 &&
            sampler.SampleValue(SamplerContext.EmptyUncached, 0, 0, 0) == 7.0f,
            "DensityFunctionCompiler must inline HolderHolder before compiling the density graph.");
    }

    private static void CheckPointCachingAndUncachedContext()
    {
        var counters = new Counters();
        var compiler = new DensityFunctionCompiler(new UnusedCompileContext());
        DensitySampler sampler = compiler.GetSampler(
            DensityFunctions.Cache(new CountingFunction(counters, 1.0f)));

        var cachedContext = SamplerContext.CreateBuilder().EnableCaches().Build();
        float first = sampler.SampleValue(cachedContext, 2, 3, 4);
        int callsAfterFirst = counters.PointCalls;
        float repeated = sampler.SampleValue(cachedContext, 2, 3, 4);

        Require(
            first == repeated &&
            callsAfterFirst == 1 &&
            counters.PointCalls == callsAfterFirst,
            "CachingDensitySampler must reuse the last point value in an enabled SamplerContext.");

        sampler.SampleValue(cachedContext, 3, 3, 4);
        sampler.SampleValue(cachedContext, 2, 3, 4);
        Require(
            counters.PointCalls == 3,
            "The point cache must retain only its last distinct point outside a cached volume.");

        int beforeUncached = counters.PointCalls;
        sampler.SampleValue(SamplerContext.EmptyUncached, 2, 3, 4);
        sampler.SampleValue(SamplerContext.EmptyUncached, 2, 3, 4);
        Require(
            counters.PointCalls == beforeUncached + 2,
            "SamplerContext.EmptyUncached must bypass every CachingDensitySampler cell.");
    }

    private static void CheckVolumeCachingAndReplacement()
    {
        var counters = new Counters();
        var compiler = new DensityFunctionCompiler(new UnusedCompileContext());
        DensitySampler sampler = compiler.GetSampler(
            DensityFunctions.Cache(new CountingFunction(counters, 0.25f)));

        var arena = new CountingArena();
        SamplerContext context = SamplerContext.CreateBuilder()
            .UseBufferArena(arena)
            .EnableCaches()
            .Build();

        var volume = new DensityVolume(2, 2, 2, 0, 10, 20);
        DensityBuffer first = DensityBuffer.CreateUnpooled(volume.Size());
        sampler.SampleVolume(context, first, volume);
        Require(
            counters.VolumeCalls == 1 &&
            counters.PointCalls == volume.Size() &&
            arena.Acquired == 1,
            "The first cached volume request must sample the input once into one scoped buffer.");

        DensityBuffer second = DensityBuffer.CreateUnpooled(volume.Size());
        sampler.SampleVolume(context, second, volume);
        Require(
            counters.VolumeCalls == 1 &&
            arena.Acquired == 1 &&
            Enumerable.Range(0, volume.Size()).All(index => first.Get(index) == second.Get(index)),
            "A repeated request for the same DensityVolume must copy the cached buffer without resampling.");

        float fromVolume = sampler.SampleValue(context, 1, 11, 21);
        Require(
            fromVolume == first.Get(volume.IndexOfBlock(1, 11, 21)) &&
            counters.PointCalls == volume.Size(),
            "Point sampling inside the cached DensityVolume must read from its cached volume buffer.");

        var changedVolume = new DensityVolume(2, 2, 2, 16, 10, 20);
        sampler.SampleVolume(context, second, changedVolume);
        Require(
            counters.VolumeCalls == 2 &&
            arena.Acquired == 2 &&
            arena.Released == 1,
            "A different DensityVolume must replace and close the previous cache-cell buffer.");
    }

    private static void CheckEqualCacheInputsReuseOnePreparedCache()
    {
        var counters = new Counters();
        DensityFunction firstInput = new CountingFunction(counters, 5.0f);
        DensityFunction equalInput = new CountingFunction(counters, 5.0f);
        Require(
            !ReferenceEquals(firstInput, equalInput) && firstInput.Equals(equalInput),
            "The cache-sharing fixture must contain distinct but structurally equal input nodes.");

        DensityFunction firstGraph = DensityFunctions.Add(
            DensityFunctions.Cache(firstInput),
            DensityFunctions.Constant(1.0f));
        DensityFunction secondGraph = DensityFunctions.Add(
            DensityFunctions.Cache(equalInput),
            DensityFunctions.Constant(2.0f));

        var compiler = new DensityFunctionCompiler(new UnusedCompileContext());
        DensitySampler firstSampler = compiler.GetSampler(firstGraph);
        DensitySampler secondSampler = compiler.GetSampler(secondGraph);

        var firstCachingSampler = RequireCachingSampler(
            RequireConstAddSampler(firstSampler).Left);
        var secondCachingSampler = RequireCachingSampler(
            RequireConstAddSampler(secondSampler).Left);

        Require(
            ReferenceEquals(firstCachingSampler, secondCachingSampler) &&
            firstCachingSampler.Id == secondCachingSampler.Id &&
            counters.CompileCalls == 1,
            "Different graph nodes with equal CacheFunction inputs must reuse one PreparedCache and cache ID.");
    }

    private static void CheckNestedCachesReceiveDistinctIds()
    {
        var counters = new Counters();
        DensityFunction nested = DensityFunctions.Cache(
            DensityFunctions.Cache(new CountingFunction(counters, 2.0f)));
        var compiler = new DensityFunctionCompiler(new UnusedCompileContext());

        var outer = RequireCachingSampler(compiler.GetSampler(nested));
        var inner = RequireCachingSampler(outer.Input);

        Require(
            outer.Id != inner.Id,
            "Nested CacheFunction nodes must receive distinct prepared cache IDs.");
    }

    private static BinaryFunction.ConstAddSampler RequireConstAddSampler(DensitySampler sampler) =>
        sampler is BinaryFunction.ConstAddSampler constantAdd
            ? constantAdd
            : throw new InvalidOperationException(
                $"Expected constant-add sampler, got {sampler.GetType().Name}.");

    private static CachingDensitySampler RequireCachingSampler(DensitySampler sampler) =>
        sampler is CachingDensitySampler caching
            ? caching
            : throw new InvalidOperationException(
                $"Expected caching sampler, got {sampler.GetType().Name}.");

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Util;
using Asteria.Util.Context;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.Synth;

internal static class DensitySamplerContextBindingChecks
{
    private sealed class CountingSampler(float value) : DensitySampler
    {
        public int PointCalls { get; private set; }
        public int VolumeCalls { get; private set; }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            PointCalls++;
            return value + blockX * 100.0f + blockY * 10.0f + blockZ;
        }

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            VolumeCalls++;
            DensitySampler.SampleVolumeNaive(context, outputBuffer, volume, this);
        }
    }

    private sealed record MarkerFunction(float Value) : DensityFunction
    {
        public DensitySampler CompileSampler(DensityFunction.CompileContext context) =>
            throw new NotSupportedException();

        public DensityFunction RewriteChildren(DfRewriteRule rule) => this;

        public Interval Range() => Interval.OfExact(Value);

        public int DomainAxes() => DensityFunction.NO_AXES;
    }

    private sealed class FixedSamplerSet(DensitySampler.Bound bound) : DensitySamplerSet
    {
        public int Lookups { get; private set; }

        public DensitySampler.Bound Get(DensityFunction function)
        {
            ArgumentNullException.ThrowIfNull(function);
            Lookups++;
            return bound;
        }
    }

    public static void Run()
    {
        CheckContextBoundSamplerFallback();
        CheckContextBoundSamplerOverride();
        CheckDensitySamplerSetLookupAndDefaultSampling();
    }

    private static void CheckContextBoundSamplerFallback()
    {
        var key = ContextKey<DensitySampler>.Vanilla("fallback_density_sampler");
        var fallback = new CountingSampler(5.0f);
        var context = SamplerContext.EmptyUncached;
        var sampler = new ContextBoundSampler(key, fallback);

        Require(
            sampler.SampleValue(context, 2, 3, 4) == 237.0f &&
            fallback.PointCalls == 1,
            "ContextBoundSampler must delegate point evaluation to the fallback when its context field is absent.");

        var volume = new DensityVolume(2, 1, 2, 0, 0, 0);
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        sampler.SampleVolume(context, output, volume);
        Require(
            fallback.VolumeCalls == 1 &&
            output.Get(volume.IndexUnchecked(1, 0, 1)) == 106.0f,
            "ContextBoundSampler must delegate volume evaluation to the fallback when its context field is absent.");
    }

    private static void CheckContextBoundSamplerOverride()
    {
        var key = ContextKey<DensitySampler>.Vanilla("selected_density_sampler");
        var fallback = new CountingSampler(1.0f);
        var selected = new CountingSampler(20.0f);
        ContextMap fields = ContextMap.CreateBuilder()
            .WithParameter(key, selected)
            .Build();
        SamplerContext context = SamplerContext.CreateBuilder()
            .SetUserFields(fields)
            .Build();

        var sampler = new ContextBoundSampler(key, fallback);
        Require(
            sampler.SampleValue(context, 1, 2, 3) == 225.0f &&
            selected.PointCalls == 1 &&
            fallback.PointCalls == 0,
            "ContextBoundSampler must resolve the context-specific sampler for point evaluation.");

        var volume = new DensityVolume(2, 1, 1, 0, 0, 0);
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        sampler.SampleVolume(context, output, volume);
        Require(
            selected.VolumeCalls == 1 &&
            fallback.VolumeCalls == 0 &&
            output.Get(1) == 121.0f,
            "ContextBoundSampler must resolve the context-specific sampler for volume evaluation.");
    }

    private static void CheckDensitySamplerSetLookupAndDefaultSampling()
    {
        var contextKey = ContextKey<DensitySampler>.Vanilla("sampler_set_selected");
        var selected = new CountingSampler(7.0f);
        SamplerContext context = SamplerContext.CreateBuilder()
            .SetUserFields(ContextMap.CreateBuilder()
                .WithParameter(contextKey, selected)
                .Build())
            .Build();
        var fallback = new CountingSampler(-10.0f);
        var bound = new ContextBoundSampler(contextKey, fallback).Bind(context);
        var set = new FixedSamplerSet(bound);
        DensityFunction first = new MarkerFunction(1.0f);
        DensityFunction second = new MarkerFunction(1.0f);

        Require(
            ((DensitySamplerSet)set).SampleValue(first, 4, 5, 6) == 456.0f &&
            selected.PointCalls == 1 &&
            fallback.PointCalls == 0 &&
            set.Lookups == 1,
            "DensitySamplerSet.sampleValue must resolve one Bound and invoke its point sampler.");

        Require(
            ((DensitySamplerSet)set).SampleValue(second, 1, 2, 3) == 123.0f &&
            selected.PointCalls == 2 &&
            set.Lookups == 2,
            "DensitySamplerSet must perform a fresh lookup for every function request.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

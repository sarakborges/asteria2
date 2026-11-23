using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Util;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Operations;
using Asteria.World.LevelGen.Synth;

internal static class InterpolatedFunctionChecks
{
    private sealed class Counters
    {
        public int CompileCalls;
        public int PointCalls;
        public int VolumeCalls;
    }

    private sealed record PolynomialFunction(Counters Counters) : DensityFunction
    {
        public DensitySampler CompileSampler(DensityFunction.CompileContext context)
        {
            Interlocked.Increment(ref Counters.CompileCalls);
            return new PolynomialSampler(Counters);
        }

        public DensityFunction RewriteChildren(DfRewriteRule rule) => this;

        public Interval Range() => Interval.INFINITE;

        public int DomainAxes() => DensityFunction.ALL_AXES;
    }

    private sealed record PolynomialSampler(Counters Counters) : DensitySampler
    {
        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            Interlocked.Increment(ref Counters.PointCalls);
            return Evaluate(blockX, blockY, blockZ);
        }

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            Interlocked.Increment(ref Counters.VolumeCalls);
            DensitySampler.SampleVolumeNaive(context, outputBuffer, volume, this);
        }

        public static float Evaluate(float x, float y, float z) =>
            1.25f + 0.75f * x - 1.5f * y + 2.25f * z +
            0.125f * x * y - 0.0625f * x * z + 0.03125f * y * z +
            0.0078125f * x * y * z;
    }

    private sealed record ThrowingFunction : DensityFunction
    {
        public DensitySampler CompileSampler(DensityFunction.CompileContext context) => new ThrowingSampler();
        public DensityFunction RewriteChildren(DfRewriteRule rule) => this;
        public Interval Range() => Interval.INFINITE;
        public int DomainAxes() => DensityFunction.ALL_AXES;
    }

    private sealed class ThrowingSampler : DensitySampler
    {
        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            throw new InvalidOperationException("Expected interpolation input failure.");

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume) =>
            throw new InvalidOperationException("Expected interpolation input failure.");
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

        public void Release(ScopedDensityBuffer buffer) => Released++;
    }

    private sealed class UnusedCompileContext : DensityFunction.CompileContext
    {
        public Noise CreateNoiseSampler(Holder<NormalNoise> parameters) =>
            throw new InvalidOperationException("Interpolation checks do not request noise.");

        public IRandomSource CreateRandom(ResourceLocation seed) =>
            throw new InvalidOperationException("Interpolation checks do not request random streams.");

#pragma warning disable CS0618
        public IRandomSource CreateEndIslandRandom() =>
            throw new InvalidOperationException("Interpolation checks do not request End Island random streams.");
#pragma warning restore CS0618
    }

    public static void Run()
    {
        CheckFloorDivisionAndModulus();
        CheckPointSampling();
        CheckDirectVolumePath();
        CheckBlockStepVolumePath();
        CheckNonBlockStepVolumePath();
        CheckMetadataAndRewrite();
        CheckConstructorValidation();
        CheckBufferCleanupOnFailure();
    }

    private static DensitySampler Compile(DensityFunction input, int cellSizeXz = 4, int cellSizeY = 4) =>
        DensityFunctions.Interpolate(input, cellSizeXz, cellSizeY)
            .CompileSampler(new UnusedCompileContext());

    private static void CheckFloorDivisionAndModulus()
    {
        Require(
            Mth.FloorDiv(-1, 4) == -1 &&
            Mth.FloorDiv(-4, 4) == -1 &&
            Mth.FloorDiv(-5, 4) == -2 &&
            Mth.FloorDiv(5, 4) == 1,
            "Mth.floorDiv must round negative coordinates toward negative infinity.");

        Require(
            Mth.FloorMod(-1, 4) == 3 &&
            Mth.FloorMod(-4, 4) == 0 &&
            Mth.FloorMod(-5, 4) == 3 &&
            Mth.FloorMod(5, 4) == 1,
            "Mth.floorMod must return a non-negative remainder for positive cell sizes.");
    }

    private static void CheckPointSampling()
    {
        var counters = new Counters();
        DensitySampler sampler = Compile(new PolynomialFunction(counters));
        SamplerContext context = SamplerContext.EmptyUncached;

        float expectedCorner = PolynomialSampler.Evaluate(-4, -4, 8);
        float corner = sampler.SampleValue(context, -4, -4, 8);
        Require(
            corner == expectedCorner &&
            counters.PointCalls == 1 &&
            counters.VolumeCalls == 0,
            "An aligned point must delegate directly to the input point sampler.");

        float expectedInterpolated = Mth.Lerp3(
            0.5f, 0.25f, 0.75f,
            PolynomialSampler.Evaluate(0, 0, 0),
            PolynomialSampler.Evaluate(4, 0, 0),
            PolynomialSampler.Evaluate(0, 4, 0),
            PolynomialSampler.Evaluate(4, 4, 0),
            PolynomialSampler.Evaluate(0, 0, 4),
            PolynomialSampler.Evaluate(4, 0, 4),
            PolynomialSampler.Evaluate(0, 4, 4),
            PolynomialSampler.Evaluate(4, 4, 4));

        float interpolated = sampler.SampleValue(context, 2, 1, 3);
        Require(
            Math.Abs(interpolated - expectedInterpolated) < 0.0001f &&
            counters.VolumeCalls == 1,
            "A non-aligned point must interpolate the eight enclosing cell-corner samples.");
    }

    private static void CheckDirectVolumePath()
    {
        var counters = new Counters();
        DensitySampler sampler = Compile(new PolynomialFunction(counters));
        var volume = new DensityVolume(3, 2, 2, -4, 0, 8, 4, 4, 4);
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());

        sampler.SampleVolume(SamplerContext.EmptyUncached, output, volume);

        Require(
            counters.VolumeCalls == 1 &&
            counters.PointCalls == volume.Size(),
            "A volume already aligned to the cell lattice must bypass interpolation and sample the input volume directly.");

        for (int index = 0; index < volume.Size(); index++)
        {
            int x = index / (volume.SizeY * volume.SizeZ);
            int remainder = index % (volume.SizeY * volume.SizeZ);
            int z = remainder / volume.SizeY;
            int y = remainder % volume.SizeY;
            float expected = PolynomialSampler.Evaluate(
                volume.BlockX(x), volume.BlockY(y), volume.BlockZ(z));
            Require(output.Get(index) == expected,
                "The direct aligned-volume path must preserve every original input sample.");
        }
    }

    private static void CheckBlockStepVolumePath()
    {
        var counters = new Counters();
        DensitySampler sampler = Compile(new PolynomialFunction(counters), 4, 3);
        var volume = new DensityVolume(7, 6, 6, -3, -2, 1);
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        var arena = new CountingArena();
        SamplerContext context = SamplerContext.CreateBuilder().UseBufferArena(arena).Build();

        sampler.SampleVolume(context, output, volume);

        DensitySampler pointSampler = Compile(new PolynomialFunction(new Counters()), 4, 3);
        for (int z = 0; z < volume.SizeZ; z++)
        for (int x = 0; x < volume.SizeX; x++)
        for (int y = 0; y < volume.SizeY; y++)
        {
            int index = volume.IndexUnchecked(x, y, z);
            float expected = pointSampler.SampleValue(
                SamplerContext.EmptyUncached,
                volume.BlockX(x), volume.BlockY(y), volume.BlockZ(z));
            Require(
                Math.Abs(output.Get(index) - expected) < 0.001f,
                "The unit-step volume path must match point interpolation at every negative/positive block coordinate.");
        }

        Require(
            counters.VolumeCalls == 1 &&
            arena.Acquired == 1 &&
            arena.Released == 1,
            "The block-step path must sample one corner volume and release its scoped buffer.");
    }

    private static void CheckNonBlockStepVolumePath()
    {
        var counters = new Counters();
        DensitySampler sampler = Compile(new PolynomialFunction(counters), 4, 3);
        var volume = new DensityVolume(3, 2, 2, -3, -2, 1, 2, 3, 2);
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        var arena = new CountingArena();
        SamplerContext context = SamplerContext.CreateBuilder().UseBufferArena(arena).Build();

        sampler.SampleVolume(context, output, volume);

        DensitySampler pointSampler = Compile(new PolynomialFunction(new Counters()), 4, 3);
        for (int z = 0; z < volume.SizeZ; z++)
        for (int x = 0; x < volume.SizeX; x++)
        for (int y = 0; y < volume.SizeY; y++)
        {
            int index = volume.IndexUnchecked(x, y, z);
            float expected = pointSampler.SampleValue(
                SamplerContext.EmptyUncached,
                volume.BlockX(x), volume.BlockY(y), volume.BlockZ(z));
            Require(
                Math.Abs(output.Get(index) - expected) < 0.001f,
                "The non-block-step path must expand, interpolate and select the requested stepped block coordinates.");
        }

        Require(
            counters.VolumeCalls == 1 &&
            arena.Acquired == 2 &&
            arena.Released == 2,
            "The non-block-step path must release both the expanded block buffer and its corner buffer.");
    }

    private static void CheckMetadataAndRewrite()
    {
        DensityFunction input = new PolynomialFunction(new Counters());
        DensityFunction interpolated = DensityFunctions.Interpolate(input, 4, 3);

        Require(
            interpolated is InterpolatedFunction function &&
            ReferenceEquals(function.Input, input) &&
            function.CellSizeXz == 4 &&
            function.CellSizeY == 3 &&
            function.Range().Equals(input.Range()) &&
            function.DomainAxes() == input.DomainAxes(),
            "InterpolatedFunction must preserve cell settings and delegate range/domain metadata.");

        Require(
            ReferenceEquals(interpolated.RewriteChildren(child => child), interpolated),
            "InterpolatedFunction.rewriteChildren must preserve identity when its child is unchanged.");

        DensityFunction replacement = DensityFunctions.Constant(8.0f);
        DensityFunction rewritten = interpolated.RewriteChildren(child =>
            ReferenceEquals(child, input) ? replacement : child);

        Require(
            rewritten is InterpolatedFunction changed &&
            ReferenceEquals(changed.Input, replacement) &&
            changed.CellSizeXz == 4 &&
            changed.CellSizeY == 3,
            "InterpolatedFunction.rewriteChildren must retain both cell dimensions when replacing its child.");
    }

    private static void CheckConstructorValidation()
    {
        RequireThrows<ArgumentOutOfRangeException>(
            () => new InterpolatedFunction(DensityFunctions.Zero(), 0, 4),
            "An XZ cell size of zero must be rejected.");
        RequireThrows<ArgumentOutOfRangeException>(
            () => new InterpolatedFunction(DensityFunctions.Zero(), 4, -1),
            "A negative Y cell size must be rejected.");
    }

    private static void CheckBufferCleanupOnFailure()
    {
        var blockArena = new CountingArena();
        DensitySampler blockSampler = Compile(new ThrowingFunction(), 4, 3);
        var blockContext = SamplerContext.CreateBuilder().UseBufferArena(blockArena).Build();
        var blockVolume = new DensityVolume(2, 2, 2, 1, 1, 1);
        RequireThrows<InvalidOperationException>(
            () => blockSampler.SampleVolume(
                blockContext,
                DensityBuffer.CreateUnpooled(blockVolume.Size()),
                blockVolume),
            "A failing input must propagate its exception from block-step sampling.");
        Require(
            blockArena.Acquired == 1 && blockArena.Released == 1,
            "The cell-corner buffer must be released when block-step input sampling fails.");

        var steppedArena = new CountingArena();
        DensitySampler steppedSampler = Compile(new ThrowingFunction(), 4, 3);
        var steppedContext = SamplerContext.CreateBuilder().UseBufferArena(steppedArena).Build();
        var steppedVolume = new DensityVolume(2, 2, 2, 1, 1, 1, 2, 2, 2);
        RequireThrows<InvalidOperationException>(
            () => steppedSampler.SampleVolume(
                steppedContext,
                DensityBuffer.CreateUnpooled(steppedVolume.Size()),
                steppedVolume),
            "A failing input must propagate its exception from non-block-step sampling.");
        Require(
            steppedArena.Acquired == 2 && steppedArena.Released == 2,
            "Both nested buffers must be released when non-block-step input sampling fails.");
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

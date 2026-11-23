using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Util;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Operations;
using Asteria.World.LevelGen.Synth;

internal static class SplineFunctionChecks
{
    private enum CoordinateAxis { X, Y, Z }

    private sealed class Counters
    {
        public int CompileCalls;
        public int PointCalls;
        public int VolumeCalls;
    }

    private sealed record CoordinateFunction(
        Counters Counters,
        CoordinateAxis Axis,
        Interval ValueRange) : DensityFunction
    {
        public DensitySampler CompileSampler(DensityFunction.CompileContext context)
        {
            Interlocked.Increment(ref Counters.CompileCalls);
            return new CoordinateSampler(Counters, Axis);
        }

        public DensityFunction RewriteChildren(DfRewriteRule rule) => this;

        public Interval Range() => ValueRange;

        public int DomainAxes() => Axis switch
        {
            CoordinateAxis.X => DensityFunction.AXIS_X,
            CoordinateAxis.Y => DensityFunction.AXIS_Y,
            CoordinateAxis.Z => DensityFunction.AXIS_Z,
            _ => DensityFunction.NO_AXES
        };
    }

    private sealed record CoordinateSampler(
        Counters Counters,
        CoordinateAxis Axis) : DensitySampler
    {
        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            Interlocked.Increment(ref Counters.PointCalls);
            return CoordinateValue(blockX, blockY, blockZ);
        }

        public void SampleVolume(
            SamplerContext context,
            DensityBuffer outputBuffer,
            DensityVolume volume)
        {
            Interlocked.Increment(ref Counters.VolumeCalls);
            int index = 0;
            for (int z = 0; z < volume.SizeZ; z++)
            for (int x = 0; x < volume.SizeX; x++)
            for (int y = 0; y < volume.SizeY; y++)
                outputBuffer.Set(
                    index++,
                    CoordinateValue(volume.BlockX(x), volume.BlockY(y), volume.BlockZ(z)));
        }

        private float CoordinateValue(int x, int y, int z) => Axis switch
        {
            CoordinateAxis.X => x,
            CoordinateAxis.Y => y,
            CoordinateAxis.Z => z,
            _ => 0.0f
        };
    }

    private sealed record ThrowingVolumeFunction(Counters Counters) : DensityFunction
    {
        public DensitySampler CompileSampler(DensityFunction.CompileContext context)
        {
            Interlocked.Increment(ref Counters.CompileCalls);
            return new ThrowingSampler();
        }

        public DensityFunction RewriteChildren(DfRewriteRule rule) => this;

        public Interval Range() => Interval.INFINITE;

        public int DomainAxes() => DensityFunction.AXIS_Y;
    }

    private sealed class ThrowingSampler : DensitySampler
    {
        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ) =>
            throw new InvalidOperationException("Expected spline child failure.");

        public void SampleVolume(
            SamplerContext context,
            DensityBuffer outputBuffer,
            DensityVolume volume) =>
            throw new InvalidOperationException("Expected spline child failure.");
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
        public Noise CreateNoiseSampler(Holder<NormalNoise> parameters) =>
            throw new InvalidOperationException("Spline checks must not resolve noise.");

        public IRandomSource CreateRandom(ResourceLocation seed) =>
            throw new InvalidOperationException("Spline checks must not create random streams.");

#pragma warning disable CS0618
        public IRandomSource CreateEndIslandRandom() =>
            throw new InvalidOperationException("Spline checks must not create End Island random streams.");
#pragma warning restore CS0618
    }

    public static void Run()
    {
        CheckPointSamplingAndMetadata();
        CheckVolumeSamplingMatchesPointSampling();
        CheckNestedCoordinateCompilation();
        CheckConstantSpline();
        CheckRewriteChildren();
        CheckBufferCleanupWhenCoordinateSamplingFails();
        CheckUncompiledCoordinateGuard();
    }

    private static DensityFunction CreateLinearSpline(
        DensityFunction coordinate,
        float coordinateStart = 0.0f,
        float coordinateEnd = 10.0f)
    {
        CubicSpline<SplineFunction.Unit> spline =
            CubicSpline<SplineFunction.Unit>.CreateBuilder(
                new SplineFunction.Coordinate(coordinate))
            .AddPoint(coordinateStart, 0.0f)
            .AddPoint(coordinateEnd, 10.0f)
            .Build();

        return DensityFunctions.Spline(spline);
    }

    private static void CheckPointSamplingAndMetadata()
    {
        var counters = new Counters();
        DensityFunction coordinate = new CoordinateFunction(
            counters,
            CoordinateAxis.X,
            Interval.Of(-100.0f, 100.0f));
        DensityFunction function = CreateLinearSpline(coordinate);

        Require(
            function is SplineFunction splineFunction &&
            splineFunction.Range().Equals(Interval.Of(0.0f, 10.0f)) &&
            splineFunction.DomainAxes() == DensityFunction.AXIS_X,
            "SplineFunction must delegate its range to the cubic spline and collect axes from all coordinate functions.");

        DensitySampler sampler = function.CompileSampler(new UnusedCompileContext());
        Require(
            Math.Abs(sampler.SampleValue(SamplerContext.EmptyUncached, 2, 0, 0) - 1.04f) < 0.00001f,
            "SplineFunction point sampling must evaluate the compiled coordinate sampler through CubicSpline.");

        sampler.SampleValue(SamplerContext.EmptyUncached, 2, 0, 0);
        Require(
            counters.CompileCalls == 1 && counters.PointCalls == 2,
            "SplineFunction must compile each coordinate occurrence once and sample it once per point input.");
    }

    private static void CheckVolumeSamplingMatchesPointSampling()
    {
        var counters = new Counters();
        DensityFunction function = CreateLinearSpline(
            new CoordinateFunction(
                counters,
                CoordinateAxis.X,
                Interval.Of(-100.0f, 100.0f)));

        DensitySampler sampler = function.CompileSampler(new UnusedCompileContext());
        var volume = new DensityVolume(3, 2, 2, 0, 0, 0);
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        var arena = new CountingArena();
        SamplerContext context = SamplerContext.CreateBuilder()
            .UseBufferArena(arena)
            .Build();

        sampler.SampleVolume(context, output, volume);

        CubicSpline<float> expectedSpline = CubicSpline<float>.CreateBuilder(BoundedFloatFunctions.Identity)
            .AddPoint(0.0f, 0.0f)
            .AddPoint(10.0f, 10.0f)
            .Build();

        for (int z = 0; z < volume.SizeZ; z++)
        for (int x = 0; x < volume.SizeX; x++)
        for (int y = 0; y < volume.SizeY; y++)
        {
            int index = volume.IndexUnchecked(x, y, z);
            float expected = CubicSpline<float>.Sample(expectedSpline, volume.BlockX(x));
            Require(
                Math.Abs(output.Get(index) - expected) < 0.00001f,
                "SplineFunction volume sampling must preserve the same cubic result as point sampling.");
        }

        Require(
            counters.VolumeCalls == 1 &&
            counters.PointCalls == 0 &&
            arena.Acquired == 1 &&
            arena.Released == 1,
            "SplineFunction must lazily sample each coordinate into one scoped volume buffer and release it after the sweep.");
    }

    private static void CheckNestedCoordinateCompilation()
    {
        var xCounters = new Counters();
        var yCounters = new Counters();
        DensityFunction x = new CoordinateFunction(
            xCounters,
            CoordinateAxis.X,
            Interval.Of(-100.0f, 100.0f));
        DensityFunction y = new CoordinateFunction(
            yCounters,
            CoordinateAxis.Y,
            Interval.Of(-100.0f, 100.0f));

        CubicSpline<SplineFunction.Unit> inner =
            CubicSpline<SplineFunction.Unit>.CreateBuilder(new SplineFunction.Coordinate(y))
                .AddPoint(0.0f, 2.0f)
                .AddPoint(10.0f, 6.0f)
                .Build();
        CubicSpline<SplineFunction.Unit> outer =
            CubicSpline<SplineFunction.Unit>.CreateBuilder(new SplineFunction.Coordinate(x))
                .AddPoint(0.0f, inner)
                .AddPoint(10.0f, 20.0f)
                .Build();

        DensitySampler sampler = DensityFunctions.Spline(outer)
            .CompileSampler(new UnusedCompileContext());
        float point = sampler.SampleValue(SamplerContext.EmptyUncached, 4, 2, 0);
        Require(
            float.IsFinite(point) &&
            xCounters.CompileCalls == 1 &&
            yCounters.CompileCalls == 1 &&
            xCounters.PointCalls == 1 &&
            yCounters.PointCalls == 1,
            "Nested spline values must compile and sample each coordinate in pre-order.");
    }

    private static void CheckConstantSpline()
    {
        DensityFunction function = DensityFunctions.Spline(
            CubicSpline<SplineFunction.Unit>.CreateConstant(4.5f));
        DensitySampler sampler = function.CompileSampler(new UnusedCompileContext());

        Require(
            sampler.SampleValue(SamplerContext.EmptyUncached, -9, 100, 8) == 4.5f,
            "A constant spline must sample without coordinate inputs.");

        var volume = new DensityVolume(2, 2, 2, 0, 0, 0);
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        sampler.SampleVolume(SamplerContext.EmptyUncached, output, volume);
        Require(
            Enumerable.Range(0, output.Size()).All(index => output.Get(index) == 4.5f),
            "A constant spline must fill every requested volume value.");
    }

    private static void CheckRewriteChildren()
    {
        var counters = new Counters();
        DensityFunction original = new CoordinateFunction(
            counters,
            CoordinateAxis.X,
            Interval.Of(-100.0f, 100.0f));
        DensityFunction spline = CreateLinearSpline(original);

        DensityFunction unchanged = spline.RewriteChildren(function => function);
        Require(
            ReferenceEquals(unchanged, spline),
            "SplineFunction.rewriteChildren must preserve identity when all coordinates are unchanged.");

        DensityFunction replacement = DensityFunctions.Constant(3.0f);
        DensityFunction rewritten = spline.RewriteChildren(
            function => ReferenceEquals(function, original) ? replacement : function);
        Require(
            rewritten is SplineFunction updated &&
            !ReferenceEquals(updated, spline) &&
            updated.DomainAxes() == DensityFunction.NO_AXES &&
            updated.Spline is CubicSpline<SplineFunction.Unit>.Multipoint multipoint &&
            multipoint.Coordinate is SplineFunction.Coordinate coordinate &&
            ReferenceEquals(coordinate.Function, replacement),
            "SplineFunction.rewriteChildren must rebuild the graph when a coordinate function changes.");
    }

    private static void CheckBufferCleanupWhenCoordinateSamplingFails()
    {
        var arena = new CountingArena();
        DensityFunction x = new CoordinateFunction(
            new Counters(),
            CoordinateAxis.X,
            Interval.Of(-100.0f, 100.0f));
        DensityFunction y = new ThrowingVolumeFunction(new Counters());

        CubicSpline<SplineFunction.Unit> inner =
            CubicSpline<SplineFunction.Unit>.CreateBuilder(new SplineFunction.Coordinate(y))
                .AddPoint(0.0f, 2.0f)
                .AddPoint(10.0f, 6.0f)
                .Build();
        CubicSpline<SplineFunction.Unit> outer =
            CubicSpline<SplineFunction.Unit>.CreateBuilder(new SplineFunction.Coordinate(x))
                .AddPoint(0.0f, inner)
                .AddPoint(10.0f, 20.0f)
                .Build();

        DensitySampler sampler = DensityFunctions.Spline(outer)
            .CompileSampler(new UnusedCompileContext());
        var volume = new DensityVolume(2, 1, 1, 4, 0, 0);
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        SamplerContext context = SamplerContext.CreateBuilder()
            .UseBufferArena(arena)
            .Build();

        try
        {
            sampler.SampleVolume(context, output, volume);
        }
        catch (InvalidOperationException exception) when (
            exception.Message == "Expected spline child failure.")
        {
            Require(
                arena.Acquired == 2 && arena.Released == 2,
                "SplineFunction must close every acquired coordinate buffer when a child sampler throws.");
            return;
        }

        throw new InvalidOperationException("A throwing spline coordinate sampler must propagate its failure.");
    }

    private static void CheckUncompiledCoordinateGuard()
    {
        var coordinate = new SplineFunction.Coordinate(DensityFunctions.Zero());
        RequireThrows<InvalidOperationException>(
            () => coordinate.Apply(default),
            "A raw spline coordinate descriptor must not be evaluated before it is compiled.");
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

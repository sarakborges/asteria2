using Asteria.Core;
using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Operations;

internal static class SliceFunctionChecks
{
    private static readonly DensityFunction.CompileContext Context = new TestCompileContext();

    private sealed class TestCompileContext : DensityFunction.CompileContext
    {
        public Asteria.World.LevelGen.Synth.Noise CreateNoiseSampler(
            Asteria.Content.Registries.Holder<Asteria.World.LevelGen.Synth.NormalNoise> parameters) =>
            throw new InvalidOperationException();

        public Asteria.World.LevelGen.IRandomSource CreateRandom(Asteria.Content.Identifiers.ResourceLocation seed) =>
            throw new InvalidOperationException();

#pragma warning disable CS0618
        public Asteria.World.LevelGen.IRandomSource CreateEndIslandRandom() =>
            throw new InvalidOperationException();
#pragma warning restore CS0618
    }

    private sealed class CoordinateSampler : DensitySampler
    {
        public int PointCalls { get; private set; }
        public int VolumeCalls { get; private set; }

        public float SampleValue(SamplerContext context, int blockX, int blockY, int blockZ)
        {
            PointCalls++;
            return Value(blockX, blockY, blockZ);
        }

        public void SampleVolume(SamplerContext context, DensityBuffer outputBuffer, DensityVolume volume)
        {
            VolumeCalls++;
            int index = 0;
            for (int z = 0; z < volume.SizeZ; z++)
                for (int x = 0; x < volume.SizeX; x++)
                    for (int y = 0; y < volume.SizeY; y++)
                        outputBuffer.Set(index++, Value(volume.BlockX(x), volume.BlockY(y), volume.BlockZ(z)));
        }

        private static float Value(int x, int y, int z) =>
            x + 10.0f * y + 100.0f * z;
    }

    private sealed record Fixture(Interval ValueRange, int Axes, CoordinateSampler Sampler) : DensityFunction
    {
        public DensitySampler CompileSampler(DensityFunction.CompileContext context) => Sampler;
        public DensityFunction RewriteChildren(DfRewriteRule rule) => this;
        public Interval Range() => ValueRange;
        public int DomainAxes() => Axes;
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

    public static void Run()
    {
        CheckScalarCoordinatesAndAxes();
        CheckXVolumeAndShortcut();
        CheckYVolume();
        CheckZVolume();
        CheckNestedXzCombination();
        CheckRangeAndRewrite();
    }

    private static DensityFunction CreateInput(CoordinateSampler sampler) =>
        new Fixture(
            Interval.Of(-1000000.0f, 1000000.0f),
            DensityFunction.ALL_AXES,
            sampler);

    private static void CheckScalarCoordinatesAndAxes()
    {
        DensityFunction input = CreateInput(new CoordinateSampler());
        DensityFunction sliceX = DensityFunctions.Slice(Direction.Axis.X, 15, input);
        DensityFunction sliceY = DensityFunctions.SliceY(input, 99);
        DensityFunction sliceZ = DensityFunctions.Slice(Direction.Axis.Z, 42, input);

        Require(Sample(sliceX, -500, 12, 3) == 15 + 120 + 300 &&
            Sample(sliceY, 5, -500, 3) == 5 + 990 + 300 &&
            Sample(sliceZ, 5, 12, -500) == 5 + 120 + 4200,
            "SliceFunction point samplers must replace only their selected coordinate.");

        Require(sliceX.DomainAxes() == (DensityFunction.AXIS_Y | DensityFunction.AXIS_Z) &&
            sliceY.DomainAxes() == (DensityFunction.AXIS_X | DensityFunction.AXIS_Z) &&
            sliceZ.DomainAxes() == (DensityFunction.AXIS_X | DensityFunction.AXIS_Y),
            "SliceFunction.domainAxes must clear only the fixed axis bit.");
    }

    private static void CheckXVolumeAndShortcut()
    {
        var sampler = new CoordinateSampler();
        DensityFunction function = DensityFunctions.Slice(Direction.Axis.X, 15, CreateInput(sampler));
        var volume = new DensityVolume(2, 3, 2, 10, 20, 30, 2, 4, 3);
        var arena = new CountingArena();
        SamplerContext context = SamplerContext.CreateBuilder().UseBufferArena(arena).Build();
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());

        function.CompileSampler(Context).SampleVolume(context, output, volume);
        AssertVolume(volume, output, (x, y, z) => 15 + 10 * y + 100 * z,
            "XSampler volume path must hold X fixed and replicate the reduced plane across X.");
        Require(arena.Acquired == 1 && arena.Released == 1,
            "XSampler must release its plane scratch buffer.");

        var shortcutVolume = new DensityVolume(1, 3, 2, 15, 20, 30, 2, 4, 3);
        var shortcutArena = new CountingArena();
        DensityBuffer shortcutOutput = DensityBuffer.CreateUnpooled(shortcutVolume.Size());
        function.CompileSampler(Context).SampleVolume(
            SamplerContext.CreateBuilder().UseBufferArena(shortcutArena).Build(),
            shortcutOutput,
            shortcutVolume);
        Require(shortcutArena.Acquired == 0 &&
            shortcutOutput.Get(0) == 15 + 200 + 3000,
            "XSampler must directly sample when the requested volume already matches the fixed X plane.");
    }

    private static void CheckYVolume()
    {
        var volume = new DensityVolume(2, 3, 2, 10, 20, 30, 2, 4, 3);
        var arena = new CountingArena();
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        DensityFunctions.SliceY(CreateInput(new CoordinateSampler()), 99)
            .CompileSampler(Context)
            .SampleVolume(SamplerContext.CreateBuilder().UseBufferArena(arena).Build(), output, volume);

        AssertVolume(volume, output, (x, y, z) => x + 990 + 100 * z,
            "YSampler volume path must sample one Y plane and replicate each X/Z cell along Y.");
        Require(arena.Acquired == 1 && arena.Released == 1,
            "YSampler must release its horizontal plane scratch buffer.");
    }

    private static void CheckZVolume()
    {
        var volume = new DensityVolume(2, 3, 2, 10, 20, 30, 2, 4, 3);
        var arena = new CountingArena();
        DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
        DensityFunctions.Slice(Direction.Axis.Z, 42, CreateInput(new CoordinateSampler()))
            .CompileSampler(Context)
            .SampleVolume(SamplerContext.CreateBuilder().UseBufferArena(arena).Build(), output, volume);

        AssertVolume(volume, output, (x, y, z) => x + 10 * y + 4200,
            "ZSampler volume path must sample one Z plane and replicate it in the original Z/X/Y output order.");
        Require(arena.Acquired == 1 && arena.Released == 1,
            "ZSampler must release its fixed-Z scratch buffer.");
    }

    private static void CheckNestedXzCombination()
    {
        var sampler = new CoordinateSampler();
        DensityFunction input = CreateInput(sampler);
        DensityFunction xThenZ = DensityFunctions.Slice(
            Direction.Axis.X,
            15,
            DensityFunctions.Slice(Direction.Axis.Z, 42, input));
        DensityFunction zThenX = DensityFunctions.Slice(
            Direction.Axis.Z,
            42,
            DensityFunctions.Slice(Direction.Axis.X, 15, input));

        Require(xThenZ.CompileSampler(Context).GetType().Name == "XzSampler" &&
            zThenX.CompileSampler(Context).GetType().Name == "XzSampler",
            "SliceFunction must combine nested perpendicular X/Z slices in either nesting order.");

        var volume = new DensityVolume(2, 3, 2, 10, 20, 30, 2, 4, 3);
        foreach (DensityFunction function in new[] { xThenZ, zThenX })
        {
            var arena = new CountingArena();
            DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size());
            function.CompileSampler(Context).SampleVolume(
                SamplerContext.CreateBuilder().UseBufferArena(arena).Build(),
                output,
                volume);
            AssertVolume(volume, output, (x, y, z) => 15 + 10 * y + 4200,
                "XzSampler must fix both horizontal coordinates and replicate the sampled Y line across X/Z.");
            Require(arena.Acquired == 1 && arena.Released == 1,
                "XzSampler must release its one-dimensional Y scratch buffer.");
        }
    }

    private static void CheckRangeAndRewrite()
    {
        DensityFunction input = CreateInput(new CoordinateSampler());
        DensityFunction slice = DensityFunctions.Slice(Direction.Axis.Y, 50, input);
        Require(slice.Range().Equals(input.Range()),
            "SliceFunction.range must preserve the child range.");
        Require(ReferenceEquals(slice.RewriteChildren(child => child), slice),
            "SliceFunction.rewriteChildren must preserve identity when its input is unchanged.");

        DensityFunction rewritten = slice.RewriteChildren(_ => DensityFunctions.Constant(5.0f));
        Require(rewritten is SliceFunction node &&
            ReferenceEquals(node.Axis, Direction.Axis.Y) &&
            node.Coordinate == 50 &&
            node.Input.Equals(DensityFunctions.Constant(5.0f)),
            "SliceFunction.rewriteChildren must preserve axis/coordinate and replace the child.");
    }

    private static float Sample(DensityFunction function, int x, int y, int z) =>
        function.CompileSampler(Context)
            .SampleValue(SamplerContext.EmptyUncached, x, y, z);

    private static void AssertVolume(
        DensityVolume volume,
        DensityBuffer output,
        Func<int, int, int, float> expected,
        string message)
    {
        for (int z = 0; z < volume.SizeZ; z++)
            for (int x = 0; x < volume.SizeX; x++)
                for (int y = 0; y < volume.SizeY; y++)
                    Require(output.Get(volume.IndexUnchecked(x, y, z)) ==
                        expected(volume.BlockX(x), volume.BlockY(y), volume.BlockZ(z)), message);
    }

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

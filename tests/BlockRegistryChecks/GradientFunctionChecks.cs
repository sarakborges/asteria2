using Asteria.Core;
using Asteria.Util;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;

internal static class GradientFunctionChecks
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

    public static void Run()
    {
        CheckTilingModesAndSerializedNames();
        CheckNegativeAndReversedCoordinates();
        CheckAxisVolumeSampling();
        CheckRangeDomainAndRewrite();
    }

    private static void CheckTilingModesAndSerializedNames()
    {
        Require(Enum.GetValues<TilingMode>().SequenceEqual(new[]
            {
                TilingMode.CLAMP_TO_EDGE,
                TilingMode.REPEAT,
                TilingMode.MIRRORED_REPEAT
            }) &&
            TilingMode.CLAMP_TO_EDGE.GetSerializedName() == "clamp_to_edge" &&
            TilingMode.REPEAT.GetSerializedName() == "repeat" &&
            TilingMode.MIRRORED_REPEAT.GetSerializedName() == "mirrored_repeat",
            "TilingMode must preserve the vanilla enum order and serialized identifiers.");

        DensityFunction clamped = DensityFunctions.Gradient(
            Direction.Axis.Y, TilingMode.CLAMP_TO_EDGE, 10, 20, 2.0f, 12.0f);
        Require(Sample(clamped, 0, 5, 0) == 2.0f &&
            Sample(clamped, 0, 15, 0) == 7.0f &&
            Sample(clamped, 0, 25, 0) == 12.0f,
            "GradientFunction.CLAMP_TO_EDGE must clamp coordinates before applying the linear gradient.");

        DensityFunction repeat = DensityFunctions.Gradient(
            Direction.Axis.X, TilingMode.REPEAT, 10, 20, 2.0f, 12.0f);
        Require(Sample(repeat, 10, 0, 0) == 2.0f &&
            Sample(repeat, 11, 0, 0) == 3.0f &&
            Sample(repeat, 20, 0, 0) == 2.0f,
            "GradientFunction.REPEAT must wrap at the coordinate range and restart from fromValue.");

        DensityFunction mirrored = DensityFunctions.Gradient(
            Direction.Axis.Z, TilingMode.MIRRORED_REPEAT, 10, 20, 2.0f, 12.0f);
        Require(Sample(mirrored, 0, 0, 10) == 2.0f &&
            Sample(mirrored, 0, 0, 15) == 7.0f &&
            Sample(mirrored, 0, 0, 20) == 12.0f &&
            Sample(mirrored, 0, 0, 25) == 7.0f &&
            Sample(mirrored, 0, 0, 30) == 2.0f,
            "GradientFunction.MIRRORED_REPEAT must reverse direction on every odd tile.");
    }

    private static void CheckNegativeAndReversedCoordinates()
    {
        DensityFunction repeat = DensityFunctions.Gradient(
            Direction.Axis.X, TilingMode.REPEAT, 0, 10, 0.0f, 10.0f);
        Require(Sample(repeat, -1, 0, 0) == 9.0f &&
            Sample(repeat, -10, 0, 0) == 0.0f &&
            Sample(repeat, -11, 0, 0) == 9.0f,
            "GradientFunction.REPEAT must use floorMod for negative coordinates.");

        DensityFunction mirrored = DensityFunctions.Gradient(
            Direction.Axis.X, TilingMode.MIRRORED_REPEAT, 0, 10, 0.0f, 10.0f);
        Require(Sample(mirrored, -1, 0, 0) == 1.0f &&
            Sample(mirrored, -10, 0, 0) == 10.0f &&
            Sample(mirrored, -11, 0, 0) == 9.0f,
            "GradientFunction.MIRRORED_REPEAT must use floorDiv and mirror negative tiles correctly.");

        DensityFunction reversed = DensityFunctions.Gradient(
            Direction.Axis.X, TilingMode.CLAMP_TO_EDGE, 20, 10, 12.0f, 2.0f);
        Require(Sample(reversed, 25, 0, 0) == 12.0f &&
            Sample(reversed, 15, 0, 0) == 7.0f &&
            Sample(reversed, 5, 0, 0) == 2.0f,
            "GradientFunction must preserve reversed from/to coordinates and value direction.");

        DensityFunction reversedRepeat = DensityFunctions.Gradient(
            Direction.Axis.X, TilingMode.REPEAT, 20, 10, 12.0f, 2.0f);
        Require(Sample(reversedRepeat, 15, 0, 0) == 7.0f,
            "GradientFunction.REPEAT must preserve floorMod semantics with a negative coordinate range.");
    }

    private static void CheckAxisVolumeSampling()
    {
        var volume = new DensityVolume(2, 3, 2, 10, 20, 30);
        foreach (Direction.Axis axis in Direction.Axis.GetValues())
        {
            DensityFunction function = DensityFunctions.Gradient(
                axis, TilingMode.CLAMP_TO_EDGE,
                10, 14, 0.0f, 4.0f);
            DensityBuffer output = DensityBuffer.CreateUnpooled(volume.Size() + 2);
            output.Fill(99.0f);
            function.CompileSampler(Context).SampleVolume(
                SamplerContext.EmptyUncached, output, volume);

            for (int z = 0; z < volume.SizeZ; z++)
            {
                for (int x = 0; x < volume.SizeX; x++)
                {
                    for (int y = 0; y < volume.SizeY; y++)
                    {
                        int blockX = volume.BlockX(x);
                        int blockY = volume.BlockY(y);
                        int blockZ = volume.BlockZ(z);
                        int coordinate = axis.Choose(blockX, blockY, blockZ);
                        int clampedCoordinate = Math.Clamp(coordinate, 10, 14);
                        float expected = clampedCoordinate - 10;
                        Require(output.Get(volume.IndexUnchecked(x, y, z)) == expected,
                            "GradientFunction sampleVolume must fill the correct axis-specific coordinate plane.");
                    }
                }
            }

            Require(output.Get(volume.Size()) == 99.0f && output.Get(volume.Size() + 1) == 99.0f,
                "GradientFunction.sampleVolume must not overwrite capacity outside the active volume.");
        }
    }

    private static void CheckRangeDomainAndRewrite()
    {
        DensityFunction gradient = DensityFunctions.YClampedGradient(0, 100, -1.0f, 1.0f);
        Require(gradient is GradientFunction node &&
            ReferenceEquals(node.Axis, Direction.Axis.Y) &&
            node.Tiling == TilingMode.CLAMP_TO_EDGE &&
            gradient.Range().Equals(Interval.Of(-1.0f, 1.0f)) &&
            gradient.DomainAxes() == DensityFunction.AXIS_Y,
            "YClampedGradient must construct a Y-axis clamped gradient with its endpoint range.");

        Require(ReferenceEquals(gradient.RewriteChildren(child => child), gradient),
            "GradientFunction.rewriteChildren must preserve its identity because it has no child functions.");
    }

    private static float Sample(DensityFunction function, int x, int y, int z) =>
        function.CompileSampler(Context)
            .SampleValue(SamplerContext.EmptyUncached, x, y, z);

    private static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }
}

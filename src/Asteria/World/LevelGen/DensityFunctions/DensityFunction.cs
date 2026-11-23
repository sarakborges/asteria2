using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Util;
using Asteria.World.LevelGen.Synth;

namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Density-function graph contract from Minecraft Java 26.3.
/// Persistence codecs are intentionally not part of this runtime-only contract slice.
/// </summary>
public interface DensityFunction
{
    const int AXIS_X = 1;
    const int AXIS_Y = 2;
    const int AXIS_Z = 4;
    const int NO_AXES = 0;
    const int ALL_AXES = 7;

    static int AxesFrom(Direction.Axis axis)
    {
        if (ReferenceEquals(axis, Direction.Axis.X))
            return AXIS_X;
        if (ReferenceEquals(axis, Direction.Axis.Y))
            return AXIS_Y;
        if (ReferenceEquals(axis, Direction.Axis.Z))
            return AXIS_Z;

        throw new ArgumentOutOfRangeException(nameof(axis));
    }

    DensityFunction Abs() => DensityFunctions.Abs(this);

    DensityFunction Square() => DensityFunctions.Square(this);

    DensityFunction Cube() => DensityFunctions.Cube(this);

    DensityFunction Sqrt() => DensityFunctions.Sqrt(this);

    DensityFunction HalfNegative() => DensityFunctions.HalfNegative(this);

    DensityFunction QuarterNegative() => DensityFunctions.QuarterNegative(this);

    DensityFunction Reciprocal() => DensityFunctions.Reciprocal(this);

    DensityFunction Negate() => DensityFunctions.Negate(this);

    DensityFunction Squeeze() => DensityFunctions.Squeeze(this);

    DensityFunction Log() => DensityFunctions.Log(this);

    DensityFunction Sign() => DensityFunctions.Sign(this);

    DensitySampler CompileSampler(CompileContext context);

    DensityFunction RewriteChildren(DfRewriteRule rule);

    Interval Range();

    int DomainAxes();

    /// <summary>
    /// Dependencies required to compile runtime samplers from the density-function graph.
    /// </summary>
    public interface CompileContext
    {
        Noise CreateNoiseSampler(Holder<NormalNoise> parameters);

        IRandomSource CreateRandom(ResourceLocation seed);

        [Obsolete("Legacy End Island density-function initialization.")]
        IRandomSource CreateEndIslandRandom();
    }
}

using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Core;
using Asteria.Util;
using Asteria.World.LevelGen.Synth;
using Asteria.Serialization;

namespace Asteria.World.LevelGen.DensityFunctions;

/// <summary>
/// Density-function graph contract and registry-aware persistence codecs from Minecraft Java 26.3.
/// </summary>
public interface DensityFunction
{
    const int AXIS_X = 1;
    const int AXIS_Y = 2;
    const int AXIS_Z = 4;
    const int NO_AXES = 0;
    const int ALL_AXES = 7;

    /// <summary>Registry references only; inline density values are not accepted.</summary>
    static Codec<Holder<DensityFunction>> REFERENCE_CODEC { get; } =
        RegistryCodecs.Holder(Registries.DENSITY_FUNCTION);

    /// <summary>Registered density references or inline density-function definitions.</summary>
    static Codec<DensityFunction> CODEC { get; } =
        RegistryCodecs.Holder(
                Registries.DENSITY_FUNCTION,
                DensityFunctions.DIRECT_CODEC)
            .XMap<DensityFunction>(
                static holder => holder.Kind == HolderKind.Direct
                    ? holder.Value
                    : new DensityFunctions.HolderHolder(holder),
                static function => function is DensityFunctions.HolderHolder holder
                    ? holder.Function
                    : Holder<DensityFunction>.Direct(function));

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

    DensityFunction Add(DensityFunction right) => DensityFunctions.Add(this, right);

    DensityFunction Add(float right) => DensityFunctions.Add(this, right);

    DensityFunction Sub(DensityFunction right) => DensityFunctions.Sub(this, right);

    DensityFunction Sub(float right) => DensityFunctions.Sub(this, right);

    DensityFunction Mul(DensityFunction right) => DensityFunctions.Mul(this, right);

    DensityFunction Mul(float right) => DensityFunctions.Mul(this, right);

    DensityFunction Div(DensityFunction right) => DensityFunctions.Div(this, right);

    DensityFunction Div(float right) => DensityFunctions.Div(this, right);

    DensityFunction Clamp(float min, float max) => DensityFunctions.Clamp(this, min, max);

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

        IRandomSource CreateEndIslandRandom();
    }
}

using Asteria.Content.Identifiers;
using Asteria.Core;
using Asteria.Serialization;
using Asteria.Util;
using Asteria.World.LevelGen;
using Asteria.World.LevelGen.DensityFunctions;
using Asteria.World.LevelGen.DensityFunctions.Generator;
using Asteria.World.LevelGen.DensityFunctions.Operations;

namespace Asteria.World.LevelGen.Synth;

/// <summary>
/// Legacy blended terrain noise from Minecraft Java 26.3.
/// </summary>
public sealed record BlendedNoise(
    double XzScale,
    double YScale,
    double XzFactor,
    double YFactor,
    double SmearScaleMultiplier) : DensityFunction
{
    private const double BASE_SCALE = 684.412;
    private const double LIMIT_FACTOR = 0.9999847412109375;
    private const double MAIN_FACTOR = 12.75;
    private const int LIMIT_FIRST_OCTAVE = -15;
    private const int MAIN_FIRST_OCTAVE = -7;

    public static ResourceLocation NOISE_SEED { get; } =
        ResourceLocation.WithDefaultNamespace("terrain");

    private static Codec<double> ScaleRange { get; } =
        Codecs.DoubleRange(0.001, 1000.0);

    private sealed record ScaleFields(
        double XzScale,
        double YScale,
        double XzFactor,
        double YFactor);

    private static MapCodec<ScaleFields> ScaleFieldsCodec { get; } =
        RecordCodecBuilder.MapCodec<ScaleFields>(instance =>
            instance.Group(
                    ScaleRange.FieldOf("xz_scale")
                        .ForGetter<ScaleFields>(static scales => scales.XzScale),
                    ScaleRange.FieldOf("y_scale")
                        .ForGetter<ScaleFields>(static scales => scales.YScale),
                    ScaleRange.FieldOf("xz_factor")
                        .ForGetter<ScaleFields>(static scales => scales.XzFactor),
                    ScaleRange.FieldOf("y_factor")
                        .ForGetter<ScaleFields>(static scales => scales.YFactor))
                .Apply(
                    instance,
                    static (xzScale, yScale, xzFactor, yFactor) =>
                        new ScaleFields(xzScale, yScale, xzFactor, yFactor)));

    public static MapCodec<BlendedNoise> CODEC { get; } =
        RecordCodecBuilder.MapCodec<BlendedNoise>(instance =>
            instance.Group(
                    ScaleFieldsCodec.ForGetter<BlendedNoise>(static noise =>
                        new ScaleFields(
                            noise.XzScale,
                            noise.YScale,
                            noise.XzFactor,
                            noise.YFactor)),
                    Codecs.DoubleRange(1.0, 8.0)
                        .FieldOf("smear_scale_multiplier")
                        .ForGetter<BlendedNoise>(static noise => noise.SmearScaleMultiplier))
                .Apply(
                    instance,
                    static (scales, smearScaleMultiplier) =>
                        new BlendedNoise(
                            scales.XzScale,
                            scales.YScale,
                            scales.XzFactor,
                            scales.YFactor,
                            smearScaleMultiplier)));

    private double XzMultiplier() =>
        BASE_SCALE * XzScale;

    private double YMultiplier() =>
        BASE_SCALE * YScale;

    public FbmSet CreateFbmSet(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);

        double limitSmearScaleY = YMultiplier() * SmearScaleMultiplier;
        double mainSmearScaleY = limitSmearScaleY / YFactor;

        return new FbmSet(
            CreateFbm(random, LIMIT_FIRST_OCTAVE, limitSmearScaleY, LIMIT_FACTOR),
            CreateFbm(random, LIMIT_FIRST_OCTAVE, limitSmearScaleY, LIMIT_FACTOR),
            CreateFbm(random, MAIN_FIRST_OCTAVE, mainSmearScaleY, MAIN_FACTOR));
    }

    public static NoiseStack CreateFbm(
        IRandomSource random,
        int firstOctave,
        double smearScaleY,
        double valueFactor)
    {
        ArgumentNullException.ThrowIfNull(random);
        if (firstOctave > 0)
            throw new ArgumentException("firstOctave>0", nameof(firstOctave));

        int octaves = -firstOctave + 1;
        double factor = 1.0;
        valueFactor /= Math.Pow(2.0, octaves) - 1.0;

        NoiseStack.Builder stack = NoiseStack.CreateBuilder();
        for (int index = octaves - 1; index >= 0; index--)
        {
            stack.Add(
                new SmearedPerlinNoise(random, smearScaleY * factor),
                factor,
                (float)valueFactor);
            factor /= 2.0;
            valueFactor *= 2.0;
        }

        return stack.Build();
    }

    private static Interval ComputeFbmRange(
        int firstOctave,
        double smearScaleY,
        double valueFactor)
    {
        int octaves = -firstOctave + 1;
        double factor = 1.0;
        valueFactor /= Math.Pow(2.0, octaves) - 1.0;
        Interval range = Interval.OfExact(0.0f);

        for (int index = octaves - 1; index >= 0; index--)
        {
            Interval layerRange = Interval.Mul(
                SmearedPerlinNoise.Range(smearScaleY * factor),
                Interval.OfExact((float)valueFactor));
            range = Interval.Add(range, layerRange);
            factor /= 2.0;
            valueFactor *= 2.0;
        }

        return range;
    }

    public DensitySampler CompileSampler(DensityFunction.CompileContext context) =>
        CompileSampler(context.CreateRandom(NOISE_SEED));

    public DensitySampler CompileSampler(IRandomSource random)
    {
        FbmSet fbms = CreateFbmSet(random);
        double xzMultiplier = XzMultiplier();
        double yMultiplier = YMultiplier();

        var minLimitNoise = new NoiseFunction.Sampler(
            fbms.MinLimitNoise,
            xzMultiplier,
            yMultiplier);
        var maxLimitNoise = new NoiseFunction.Sampler(
            fbms.MaxLimitNoise,
            xzMultiplier,
            yMultiplier);
        var mainNoise = new NoiseFunction.Sampler(
            fbms.MainNoise,
            xzMultiplier / XzFactor,
            yMultiplier / YFactor);

        var choice = new ClampFunction.Sampler(
            new BinaryFunction.ConstAddSampler(mainNoise, 0.5f),
            0.0f,
            1.0f);

        return new LerpFunction.Sampler(
            choice,
            minLimitNoise,
            maxLimitNoise);
    }

    public DensityFunction RewriteChildren(DfRewriteRule rule) =>
        this;

    public Interval Range() =>
        ComputeFbmRange(
            LIMIT_FIRST_OCTAVE,
            YMultiplier() * SmearScaleMultiplier,
            LIMIT_FACTOR);

    public int DomainAxes() =>
        DensityFunction.ALL_AXES;

    public sealed record FbmSet(
        NoiseStack MinLimitNoise,
        NoiseStack MaxLimitNoise,
        NoiseStack MainNoise);
}

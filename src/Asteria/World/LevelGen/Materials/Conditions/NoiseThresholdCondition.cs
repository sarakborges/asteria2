using Asteria.Content.Identifiers;
using Asteria.Content.Registries;
using Asteria.Serialization;
using Asteria.World.LevelGen.Materials;
using Asteria.World.LevelGen.Synth;

namespace Asteria.World.LevelGen.Materials.Conditions;

/// <summary>Tests a named normal-noise sample against an inclusive threshold interval.</summary>
public sealed record NoiseThresholdCondition(
    RegistryKey<NormalNoise> Noise,
    double MinThreshold,
    double MaxThreshold,
    bool Is3D) : MaterialCondition
{
    private static Codec<ResourceLocation> ResourceLocationCodec { get; } =
        Codecs.STRING.FlatXMap(
            static value => ResourceLocation.TryParse(value) is ResourceLocation identifier
                ? DataResult<ResourceLocation>.Success(identifier)
                : DataResult<ResourceLocation>.Error(() => $"Invalid noise registry key '{value}'"),
            static identifier => DataResult<string>.Success(identifier.ToString()));

    private static Codec<RegistryKey<NormalNoise>> NoiseKeyCodec { get; } =
        ResourceLocationCodec.FlatXMap(
            static identifier => DataResult<RegistryKey<NormalNoise>>.Success(
                RegistryKey<NormalNoise>.Create(Registries.NOISE, identifier)),
            static key => key.IsFor(Registries.NOISE)
                ? DataResult<ResourceLocation>.Success(key.Location)
                : DataResult<ResourceLocation>.Error(
                    () => $"Noise key {key} does not belong to {Registries.NOISE}"));

    public static MapCodec<NoiseThresholdCondition> CODEC { get; } =
        RecordCodecBuilder.MapCodec<NoiseThresholdCondition>(instance =>
            instance.Group(
                    NoiseKeyCodec.FieldOf("noise")
                        .ForGetter<NoiseThresholdCondition>(static condition => condition.Noise),
                    Codecs.DOUBLE.FieldOf("min_threshold")
                        .ForGetter<NoiseThresholdCondition>(static condition => condition.MinThreshold),
                    Codecs.DOUBLE.FieldOf("max_threshold")
                        .ForGetter<NoiseThresholdCondition>(static condition => condition.MaxThreshold),
                    Codecs.BOOL.OptionalFieldOf("is_3d", false)
                        .ForGetter<NoiseThresholdCondition>(static condition => condition.Is3D))
                .Apply(
                    instance,
                    static (noise, minimum, maximum, is3d) =>
                        new NoiseThresholdCondition(noise, minimum, maximum, is3d)));

    public static MapCodec<MaterialCondition> TYPE_CODEC { get; } =
        CODEC.XMap<MaterialCondition>(
            static condition => condition,
            static condition => (NoiseThresholdCondition)condition);

    public ConditionEvaluator Compile(MaterialRuleContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        Func<double> noise = context.GetNoiseSampler(Noise, Is3D);
        return () =>
        {
            double value = noise();
            return value >= MinThreshold && value <= MaxThreshold;
        };
    }

    public MapCodec<MaterialCondition> Codec() => TYPE_CODEC;
}

using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Constant world-generation float provider from Minecraft Java 26.3.</summary>
public sealed record ConstantFloat(float Value) : FloatProvider
{
    public static ConstantFloat ZERO { get; } = new(0.0f);

    public static MapCodec<ConstantFloat> MAP_CODEC { get; } =
        RecordCodecBuilder.MapCodec<ConstantFloat>(instance =>
            instance.Group(
                    Codecs.FLOAT.FieldOf("value")
                        .ForGetter<ConstantFloat>(static provider => provider.Value))
                .Apply(instance, static value => Of(value)));

    public static MapCodec<FloatProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<FloatProvider>(
            static provider => provider,
            static provider => (ConstantFloat)provider);

    public static ConstantFloat Of(float value) =>
        value == 0.0f ? ZERO : new ConstantFloat(value);

    public float Sample(IRandomSource random) => Value;

    public float Min() => Value;

    public float Max() => Value;

    public MapCodec<FloatProvider> Codec() => TYPE_CODEC;

    public override string ToString() =>
        Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

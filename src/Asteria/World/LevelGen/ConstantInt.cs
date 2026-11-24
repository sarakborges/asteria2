using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Constant world-generation integer provider from Minecraft Java 26.3.</summary>
public sealed record ConstantInt(int Value) : IntProvider
{
    public static ConstantInt ZERO { get; } = new(0);

    public static MapCodec<ConstantInt> MAP_CODEC { get; } =
        RecordCodecBuilder.MapCodec<ConstantInt>(instance =>
            instance.Group(
                    Codecs.INT.FieldOf("value")
                        .ForGetter<ConstantInt>(static provider => provider.Value))
                .Apply(instance, static value => Of(value)));

    public static MapCodec<IntProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<IntProvider>(
            static provider => provider,
            static provider => (ConstantInt)provider);

    public static ConstantInt Of(int value) =>
        value == 0 ? ZERO : new ConstantInt(value);

    public int Sample(IRandomSource random) => Value;

    public int MinInclusive() => Value;

    public int MaxInclusive() => Value;

    public MapCodec<IntProvider> Codec() => TYPE_CODEC;

    public override string ToString() =>
        Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
}

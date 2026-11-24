using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Quadratically biased integer provider from Minecraft Java 26.3.</summary>
public sealed record BiasedToBottomInt(int Minimum, int Maximum) : IntProvider
{
    public static MapCodec<BiasedToBottomInt> MAP_CODEC { get; } =
        RecordCodecBuilder.MapCodec<BiasedToBottomInt>(instance =>
            instance.Group(
                    Codecs.INT.FieldOf("min_inclusive")
                        .ForGetter<BiasedToBottomInt>(static provider => provider.Minimum),
                    Codecs.INT.FieldOf("max_inclusive")
                        .ForGetter<BiasedToBottomInt>(static provider => provider.Maximum))
                .Apply(
                    instance,
                    static (minimum, maximum) => new BiasedToBottomInt(minimum, maximum)))
        .Validate(static provider =>
            provider.Maximum < provider.Minimum
                ? DataResult<BiasedToBottomInt>.Error(() =>
                    $"Max must be at least min, min_inclusive: {provider.Minimum}, max_inclusive: {provider.Maximum}")
                : DataResult<BiasedToBottomInt>.Success(provider));

    public static MapCodec<IntProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<IntProvider>(
            static provider => provider,
            static provider => (BiasedToBottomInt)provider);

    public static BiasedToBottomInt Of(int minimum, int maximum) =>
        new(minimum, maximum);

    public int Sample(IRandomSource random)
    {
        ArgumentNullException.ThrowIfNull(random);
        int span = unchecked(Maximum - Minimum + 1);
        return unchecked(Minimum + random.NextInt(random.NextInt(span) + 1));
    }

    public int MinInclusive() => Minimum;

    public int MaxInclusive() => Maximum;

    public MapCodec<IntProvider> Codec() => TYPE_CODEC;

    public override string ToString() => $"[{Minimum}-{Maximum}]";
}

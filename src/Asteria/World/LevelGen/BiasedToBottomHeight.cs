using System.Diagnostics;
using Asteria.Serialization;

namespace Asteria.World.LevelGen;

/// <summary>Height distribution biased toward the lower anchor.</summary>
public sealed record BiasedToBottomHeight(
    VerticalAnchor Minimum,
    VerticalAnchor Maximum,
    int Inner) : HeightProvider
{
    public static MapCodec<BiasedToBottomHeight> MAP_CODEC { get; } =
        RecordCodecBuilder.MapCodec<BiasedToBottomHeight>(instance =>
            instance.Group(
                    VerticalAnchor.CODEC.FieldOf("min_inclusive")
                        .ForGetter<BiasedToBottomHeight>(static provider => provider.Minimum),
                    VerticalAnchor.CODEC.FieldOf("max_inclusive")
                        .ForGetter<BiasedToBottomHeight>(static provider => provider.Maximum),
                    Codecs.IntRange(1, int.MaxValue)
                        .OptionalFieldOf("inner", 1)
                        .ForGetter<BiasedToBottomHeight>(static provider => provider.Inner))
                .Apply(
                    instance,
                    static (minimum, maximum, inner) =>
                        new BiasedToBottomHeight(minimum, maximum, inner)));

    public static MapCodec<HeightProvider> TYPE_CODEC { get; } =
        MAP_CODEC.XMap<HeightProvider>(
            static provider => provider,
            static provider => (BiasedToBottomHeight)provider);

    public static BiasedToBottomHeight Of(
        VerticalAnchor minimum,
        VerticalAnchor maximum,
        int inner) =>
        new(minimum, maximum, inner);

    public override int Sample(IRandomSource random, WorldGenerationContext context)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(context);

        int minimum = Minimum.ResolveY(context);
        int maximum = Maximum.ResolveY(context);
        int span = unchecked(maximum - minimum - Inner + 1);
        if (span <= 0)
        {
            Trace.TraceWarning("Empty height range: {0}", this);
            return minimum;
        }

        int limit = random.NextInt(span);
        return unchecked(random.NextInt(unchecked(limit + Inner)) + minimum);
    }

    public override MapCodec<HeightProvider> Codec() => TYPE_CODEC;

    public override string ToString() =>
        $"biased[{Minimum}-{Maximum} inner: {Inner}]";
}
